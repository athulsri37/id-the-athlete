using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Generates a short AI trivia blurb about the mystery player once a game ends.
// Purely cosmetic — any failure (missing key, network error, bad response)
// must fall back to null rather than break the core game.
public class AiTriviaService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string DefaultModel = "claude-sonnet-5";

    private const int MaxAttempts = 3;
    private const int DefaultRetryAfterSeconds = 10;
    private const int MaxRateLimitCooldownSeconds = 30;
    private static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromSeconds(5);

    // Cached alongside the moment it was generated so a lookup can tell a
    // still-fresh blurb apart from one whose player was edited afterward
    // (see IsStale). Only ever written on a successful generation -- see
    // GenerateBlurbAsync -- so a failed attempt never poisons this cache
    // with a permanent null.
    private sealed record CachedBlurb(string Blurb, DateTime CachedAt);
    private static readonly ConcurrentDictionary<int, CachedBlurb> Cache = new();

    // Shared across every AiTriviaService instance (the DI container hands
    // out a new transient instance per request via AddHttpClient<T>, so
    // this must be static to actually function as a circuit breaker rather
    // than resetting on every call). Guarded by RateLimitGate since
    // concurrent requests can race to read/write it.
    private static DateTime _rateLimitedUntilUtc = DateTime.MinValue;
    private static readonly object RateLimitGate = new();

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly GameDbContext _db;

    public AiTriviaService(HttpClient httpClient, IConfiguration configuration, GameDbContext db)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _db = db;
    }

    public async Task<string?> GetTriviaBlurbAsync(Player player)
    {
        // Checked ahead of the cache lookup (not just the API key check) so a
        // disabled flag never gets baked into a cached null — otherwise
        // re-enabling later would leave every already-seen player stuck null.
        if (!await IsEnabledAsync())
            return null;

        if (Cache.TryGetValue(player.Id, out var cached) && !IsStale(player, cached))
            return cached.Blurb;

        return await GenerateBlurbAsync(player);
    }

    // A cached blurb is stale once the player has been edited (via the
    // admin tool) after it was generated. A never-edited player
    // (LastModifiedAt == null) can never be stale.
    private static bool IsStale(Player player, CachedBlurb cached)
        => player.LastModifiedAt is { } lastModified && lastModified > cached.CachedAt;

    private async Task<bool> IsEnabledAsync()
    {
        try
        {
            var value = await _db.AppSettings
                .Where(s => s.Key == "AiTriviaEnabled")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    private async Task<string?> GenerateBlurbAsync(Player player)
    {
        try
        {
            // Circuit breaker: if a prior call in this process was
            // rate-limited recently, skip the network call entirely rather
            // than immediately failing another request against the same
            // limit.
            if (IsCircuitOpen())
                return null;

            var apiKey = _configuration["Anthropic:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return null;

            var model = _configuration["Anthropic:Model"];
            if (string.IsNullOrWhiteSpace(model))
                model = DefaultModel;

            // player.Sport may or may not be Included by the caller, but
            // player.SportId (the scalar FK) is always populated by EF
            // Core regardless -- looking it up directly here means this
            // service never depends on a specific caller's query shape.
            var sportName = await _db.Sports
                .Where(s => s.Id == player.SportId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();

            // e.g. "Men's Tennis player" or "Men's International Cricket
            // player" -- never a hardcoded sport. Falls back to a still-
            // correct generic phrasing if the sport can't be resolved
            // (shouldn't happen given FK integrity) rather than silently
            // re-introducing a "tennis player" assumption.
            var playerDescription = string.IsNullOrWhiteSpace(sportName) ? "athlete" : $"{sportName} player";

            var stats = string.Join(", ", player.AttributeValues
                .Where(v => v.AttributeDefinition != null)
                .Select(v => $"{v.AttributeDefinition!.Label}: {v.Value}"));

            var prompt = $"Write a short, engaging trivia blurb (exactly 2 sentences) about the {playerDescription} {player.Name}. " +
                         $"Use these stats as context: {stats}. Return only the blurb text, with no preamble or quotation marks.";

            var requestBody = new
            {
                model,
                max_tokens = 150,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                HttpResponseMessage response;
                try
                {
                    using var httpRequest = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl)
                    {
                        Content = JsonContent.Create(requestBody)
                    };
                    httpRequest.Headers.Add("x-api-key", apiKey);
                    httpRequest.Headers.Add("anthropic-version", AnthropicVersion);

                    response = await _httpClient.SendAsync(httpRequest);
                }
                catch when (attempt < MaxAttempts)
                {
                    // Network error or timeout -- treated the same as a
                    // retriable 5xx below. On the final attempt this falls
                    // through to the outer catch instead, which returns
                    // null without caching anything.
                    await Task.Delay(BackoffDelay(attempt));
                    continue;
                }

                using (response)
                {
                    if (response.IsSuccessStatusCode)
                    {
                        var payload = await response.Content.ReadFromJsonAsync<AnthropicMessageResponse>();
                        var text = payload?.Content?.FirstOrDefault()?.Text?.Trim();

                        if (string.IsNullOrWhiteSpace(text))
                            return null;

                        ClearCircuit();
                        Cache[player.Id] = new CachedBlurb(text, DateTime.UtcNow);
                        return text;
                    }

                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        OpenCircuitFromRetryAfter(response);
                        return null;
                    }

                    var isRetriableServerError = response.StatusCode is HttpStatusCode.InternalServerError
                        or HttpStatusCode.BadGateway
                        or HttpStatusCode.ServiceUnavailable;

                    if (!isRetriableServerError || attempt == MaxAttempts)
                        return null;

                    await Task.Delay(BackoffDelay(attempt));
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsCircuitOpen()
    {
        lock (RateLimitGate)
        {
            return DateTime.UtcNow < _rateLimitedUntilUtc;
        }
    }

    private static void ClearCircuit()
    {
        lock (RateLimitGate)
        {
            _rateLimitedUntilUtc = DateTime.MinValue;
        }
    }

    private static void OpenCircuitFromRetryAfter(HttpResponseMessage response)
    {
        var retryAfterSeconds = DefaultRetryAfterSeconds;

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
            retryAfterSeconds = (int)Math.Max(0, delta.TotalSeconds);
        else if (retryAfter?.Date is { } date)
            retryAfterSeconds = (int)Math.Max(0, (date - DateTimeOffset.UtcNow).TotalSeconds);

        var cooldownSeconds = Math.Min(retryAfterSeconds, MaxRateLimitCooldownSeconds);

        lock (RateLimitGate)
        {
            _rateLimitedUntilUtc = DateTime.UtcNow.AddSeconds(cooldownSeconds);
        }
    }

    // attempt 1 failed -> ~0.5s before attempt 2; attempt 2 failed -> ~1.5s
    // before attempt 3; either way capped at MaxBackoffDelay.
    private static TimeSpan BackoffDelay(int attemptJustFailed)
    {
        var delay = attemptJustFailed == 1 ? TimeSpan.FromSeconds(0.5) : TimeSpan.FromSeconds(1.5);
        return delay > MaxBackoffDelay ? MaxBackoffDelay : delay;
    }

    private class AnthropicMessageResponse
    {
        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }
    }

    private class AnthropicContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
