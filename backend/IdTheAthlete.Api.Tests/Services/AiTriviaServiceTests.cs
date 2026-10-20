using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

public class AiTriviaServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private int _next;
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var respond = responses[Math.Min(_next++, responses.Length - 1)];
            return Task.FromResult(respond());
        }
    }

    private static HttpResponseMessage RateLimited(int retryAfterSeconds)
    {
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds));
        return response;
    }

    private static HttpResponseMessage Blurb(string text) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($$"""{"content":[{"type":"text","text":"{{text}}"}]}""", Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage AnthropicError(HttpStatusCode status, string type, string message) => new(status)
    {
        Content = new StringContent($$$"""{"type":"error","error":{"type":"{{{type}}}","message":"{{{message}}}"}}""", Encoding.UTF8, "application/json")
    };

    private static (AiTriviaService service, FakeTimeProvider time) Create(ScriptedHandler handler)
        => Create(handler, new CapturingLogger<AiTriviaService>());

    private static (AiTriviaService service, FakeTimeProvider time) Create(ScriptedHandler handler,
        CapturingLogger<AiTriviaService> logger, string? apiKey = "test-key")
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = "tennis-men" });
        db.AppSettings.Add(new AppSetting { Key = "AiTriviaEnabled", Value = "true" });
        db.SaveChanges();

        var options = Options.Create(new AnthropicOptions { ApiKey = apiKey });
        var time = new FakeTimeProvider(Start);

        return (new AiTriviaService(new HttpClient(handler), options, db, new AiTriviaState(), time, new SettingsService(db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(TimeProvider.System)), logger), time);
    }

    private static Player PlayerWithId(int id) => new() { Id = id, SportId = 1, Name = $"Player {id}" };

    [Fact]
    public async Task Circuit_breaker_closes_once_the_rate_limit_cooldown_has_passed()
    {
        var handler = new ScriptedHandler(() => RateLimited(retryAfterSeconds: 30), () => Blurb("Back online."));
        var (service, time) = Create(handler);
        var realClock = Stopwatch.StartNew();

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(1)));
        Assert.Equal(1, handler.Calls);

        // Circuit is open: no network call at all.
        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(2)));
        Assert.Equal(1, handler.Calls);

        time.Advance(TimeSpan.FromSeconds(29));
        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(2)));
        Assert.Equal(1, handler.Calls);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal("Back online.", await service.GetTriviaBlurbAsync(PlayerWithId(2)));
        Assert.Equal(2, handler.Calls);

        // 31 seconds passed on the fake clock; almost none on the real one.
        Assert.True(realClock.Elapsed < TimeSpan.FromSeconds(10), $"took {realClock.Elapsed} of real time");
    }

    [Fact]
    public async Task Cached_blurb_is_reused_until_the_player_is_edited_after_it_was_cached()
    {
        var handler = new ScriptedHandler(() => Blurb("First blurb."), () => Blurb("Regenerated blurb."));
        var (service, time) = Create(handler);
        var player = PlayerWithId(7);

        Assert.Equal("First blurb.", await service.GetTriviaBlurbAsync(player));

        time.Advance(TimeSpan.FromHours(6));
        Assert.Equal("First blurb.", await service.GetTriviaBlurbAsync(player));
        Assert.Equal(1, handler.Calls);

        time.Advance(TimeSpan.FromMinutes(1));
        player.LastModifiedAt = time.GetUtcNow().UtcDateTime;
        Assert.Equal("Regenerated blurb.", await service.GetTriviaBlurbAsync(player));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task A_rejected_call_logs_Anthropics_reason_without_the_key()
    {
        var handler = new ScriptedHandler(() => AnthropicError(HttpStatusCode.Unauthorized, "authentication_error", "invalid x-api-key"));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(3)));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("401", warning.Message);
        Assert.Contains("authentication_error: invalid x-api-key", warning.Message);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("test-key"));
        Assert.Equal(1, handler.Calls); // a 401 isn't retried
    }

    [Fact]
    public async Task An_unknown_model_is_logged()
    {
        var handler = new ScriptedHandler(() => AnthropicError(HttpStatusCode.NotFound, "not_found_error", "model: claude-nope"));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(3)));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("404") && e.Message.Contains("not_found_error: model: claude-nope"));
    }

    [Fact]
    public async Task A_missing_key_is_logged_and_nothing_is_called()
    {
        var handler = new ScriptedHandler(() => Blurb("Never asked for."));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger, apiKey: null);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(3)));

        Assert.Equal(0, handler.Calls);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("no Anthropic API key"));
    }

    [Fact]
    public async Task A_rate_limit_logs_the_pause()
    {
        var handler = new ScriptedHandler(() => RateLimited(retryAfterSeconds: 12));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(3)));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("rate-limited") && e.Message.Contains("12 seconds"));
    }

    [Fact]
    public async Task Server_errors_log_each_retry_and_the_final_failure()
    {
        var handler = new ScriptedHandler(() => AnthropicError(HttpStatusCode.InternalServerError, "api_error", "Internal server error"));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, time) = Create(handler, logger);

        // Backoff waits on the fake clock: keep advancing it until the call finishes.
        var call = service.GetTriviaBlurbAsync(PlayerWithId(3));
        while (!call.IsCompleted)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        Assert.Null(await call);
        Assert.Equal(3, handler.Calls);
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message).ToList();
        Assert.Equal(3, warnings.Count);
        Assert.Contains("retrying", warnings[0]);
        Assert.Contains("retrying", warnings[1]);
        Assert.Contains("failed: Anthropic answered 500. api_error: Internal server error", warnings[2]);
    }

    [Fact]
    public async Task A_network_failure_is_logged()
    {
        var handler = new ScriptedHandler(() => throw new HttpRequestException("No such host is known."));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, time) = Create(handler, logger);

        var call = service.GetTriviaBlurbAsync(PlayerWithId(3));
        while (!call.IsCompleted)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        Assert.Null(await call);
        var warnings = logger.Entries.Where(e => e.Level == LogLevel.Warning).ToList();
        Assert.Equal(3, warnings.Count);
        Assert.Contains("No such host is known.", warnings[0].Message);
        Assert.IsType<HttpRequestException>(warnings[2].Exception);
    }

    [Fact]
    public async Task A_generated_blurb_is_logged_once_with_the_model()
    {
        var handler = new ScriptedHandler(() => Blurb("A blurb."));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Equal("A blurb.", await service.GetTriviaBlurbAsync(PlayerWithId(3)));
        Assert.Equal("A blurb.", await service.GetTriviaBlurbAsync(PlayerWithId(3))); // cached: no second log

        var info = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, info.Level);
        Assert.Contains(AnthropicOptions.DefaultModel, info.Message);
    }
}
