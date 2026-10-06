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
public class AiTriviaService : IAiTriviaService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string DefaultModel = "claude-sonnet-5";

    private const int MaxAttempts = 3;
    private const int DefaultRetryAfterSeconds = 10;
    private const int MaxRateLimitCooldownSeconds = 30;
    private static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly GameDbContext _db;
    // Cache and circuit breaker shared across instances -- see AiTriviaState.
    private readonly AiTriviaState _state;
    private readonly TimeProvider _timeProvider;
    private readonly ISettingsService _settings;

    public AiTriviaService(HttpClient httpClient, IConfiguration configuration, GameDbContext db,
        AiTriviaState state, TimeProvider timeProvider, ISettingsService settings)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _db = db;
        _state = state;
        _timeProvider = timeProvider;
        _settings = settings;
    }

    public async Task<string?> GetTriviaBlurbAsync(Player player)
    {
        // Checked ahead of the cache lookup (not just the API key check) so a
        // disabled flag never gets baked into a cached null — otherwise
        // re-enabling later would leave every already-seen player stuck null.
        if (!await _settings.IsEnabledAsync("AiTriviaEnabled"))
            return null;

        if (_state.Cache.TryGetValue(player.Id, out var cached) && !IsStale(player, cached))
            return cached.Blurb;

        return await GenerateBlurbAsync(player);
    }

    // A cached blurb is stale once the player has been edited (via the
    // admin tool) after it was generated. A never-edited player
    // (LastModifiedAt == null) can never be stale.
    private static bool IsStale(Player player, CachedTriviaBlurb cached)
        => player.LastModifiedAt is { } lastModified && lastModified > cached.CachedAt;

    private async Task<string?> GenerateBlurbAsync(Player player)
    {
        try
        {
            // Circuit breaker: if a prior call in this process was
            // rate-limited recently, skip the network call entirely rather
            // than immediately failing another request against the same
            // limit.
            if (_state.IsCircuitOpen(_timeProvider.GetUtcNow()))
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
                    await Task.Delay(BackoffDelay(attempt), _timeProvider);
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

                        _state.ClearCircuit();
                        _state.Cache[player.Id] = new CachedTriviaBlurb(text, _timeProvider.GetUtcNow().UtcDateTime);
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

                    await Task.Delay(BackoffDelay(attempt), _timeProvider);
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void OpenCircuitFromRetryAfter(HttpResponseMessage response)
    {
        var now = _timeProvider.GetUtcNow();
        var retryAfterSeconds = DefaultRetryAfterSeconds;

        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
            retryAfterSeconds = (int)Math.Max(0, delta.TotalSeconds);
        else if (retryAfter?.Date is { } date)
            retryAfterSeconds = (int)Math.Max(0, (date - now).TotalSeconds);

        var cooldownSeconds = Math.Min(retryAfterSeconds, MaxRateLimitCooldownSeconds);

        _state.OpenCircuitUntil(now.AddSeconds(cooldownSeconds));
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
