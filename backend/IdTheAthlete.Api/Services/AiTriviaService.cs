using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Generates a short AI trivia blurb about the mystery player once a game ends.
// Purely cosmetic — any failure (missing key, network error, bad response)
// must fall back to null rather than break the core game. Every failure is
// logged as a warning with Anthropic's own reason (never the key), since the
// player only ever sees no blurb.
public class AiTriviaService : IAiTriviaService
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    private const int MaxAttempts = 3;
    private const int DefaultRetryAfterSeconds = 10;
    private const int MaxRateLimitCooldownSeconds = 30;
    private static readonly TimeSpan MaxBackoffDelay = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly AnthropicOptions _options;
    private readonly GameDbContext _db;
    // Cache and circuit breaker shared across instances -- see AiTriviaState.
    private readonly AiTriviaState _state;
    private readonly TimeProvider _timeProvider;
    private readonly ISettingsService _settings;
    private readonly ILogger<AiTriviaService> _logger;

    public AiTriviaService(HttpClient httpClient, IOptions<AnthropicOptions> options, GameDbContext db,
        AiTriviaState state, TimeProvider timeProvider, ISettingsService settings, ILogger<AiTriviaService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _db = db;
        _state = state;
        _timeProvider = timeProvider;
        _settings = settings;
        _logger = logger;
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

            var apiKey = _options.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("AI trivia is on (AiTriviaEnabled) but no Anthropic API key is configured (Anthropic:ApiKey); no blurb for player {PlayerId}.", player.Id);
                return null;
            }

            var model = string.IsNullOrWhiteSpace(_options.Model) ? AnthropicOptions.DefaultModel : _options.Model;

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

            // No stats from the clue grid: given them, the model mostly
            // repeated numbers the player had just seen, so it's left to
            // write from what it knows about the player.
            var prompt = $"Write a short, engaging trivia blurb (exactly 2 sentences) about the {playerDescription} {player.Name}. " +
                         "Return only the blurb text, with no preamble or quotation marks.";

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
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    _logger.LogWarning("AI trivia call for player {PlayerId} failed (attempt {Attempt} of {MaxAttempts}), retrying: {Error}",
                        player.Id, attempt, MaxAttempts, ex.Message);
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
                        {
                            _logger.LogWarning("AI trivia for player {PlayerId}: Anthropic answered with no text.", player.Id);
                            return null;
                        }

                        _state.ClearCircuit();
                        _state.Cache[player.Id] = new CachedTriviaBlurb(text, _timeProvider.GetUtcNow().UtcDateTime);
                        _logger.LogInformation("Generated AI trivia for player {PlayerId} with {Model}.", player.Id, model);
                        return text;
                    }

                    var reason = await ReadErrorReasonAsync(response);

                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        var cooldown = OpenCircuitFromRetryAfter(response);
                        _logger.LogWarning("AI trivia for player {PlayerId} was rate-limited by Anthropic; pausing trivia for {Seconds} seconds. {Reason}",
                            player.Id, cooldown, reason);
                        return null;
                    }

                    var isRetriableServerError = response.StatusCode is HttpStatusCode.InternalServerError
                        or HttpStatusCode.BadGateway
                        or HttpStatusCode.ServiceUnavailable;

                    if (!isRetriableServerError || attempt == MaxAttempts)
                    {
                        _logger.LogWarning("AI trivia for player {PlayerId} failed: Anthropic answered {StatusCode}. {Reason}",
                            player.Id, (int)response.StatusCode, reason);
                        return null;
                    }

                    _logger.LogWarning("AI trivia for player {PlayerId}: Anthropic answered {StatusCode} (attempt {Attempt} of {MaxAttempts}), retrying. {Reason}",
                        player.Id, (int)response.StatusCode, attempt, MaxAttempts, reason);

                    await Task.Delay(BackoffDelay(attempt), _timeProvider);
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI trivia for player {PlayerId} failed.", player.Id);
            return null;
        }
    }

    // Anthropic's error body is {"type":"error","error":{"type":"...","message":"..."}},
    // e.g. authentication_error, or not_found_error for an unknown model. It
    // never contains the API key. Empty when the body isn't that shape.
    private static async Task<string> ReadErrorReasonAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<AnthropicErrorResponse>();
            return body?.Error is { } error ? $"{error.Type}: {error.Message}" : "";
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return "";
        }
    }

    // Returns the pause, in seconds.
    private int OpenCircuitFromRetryAfter(HttpResponseMessage response)
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
        return cooldownSeconds;
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

    private class AnthropicErrorResponse
    {
        [JsonPropertyName("error")]
        public AnthropicError? Error { get; set; }
    }

    private class AnthropicError
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
