using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            var respond = responses[Math.Min(_next++, responses.Length - 1)];
            return respond();
        }
    }

    private static HttpResponseMessage RateLimited(int retryAfterSeconds)
    {
        var response = new HttpResponseMessage((HttpStatusCode)429);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfterSeconds));
        return response;
    }

    private static HttpResponseMessage Blurb(string text, string stopReason = "end_turn") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        {
            content = new[] { new { type = "text", text } },
            stop_reason = stopReason,
        }), Encoding.UTF8, "application/json")
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
    public async Task The_prompt_names_the_player_and_tour_but_gives_no_stats()
    {
        var handler = new ScriptedHandler(() => Blurb("A blurb."));
        var (service, _) = Create(handler);
        var player = new Player
        {
            Id = 4, SportId = 1, Name = "Pete Sampras",
            AttributeValues =
            [
                new PlayerAttributeValue { Value = "14", AttributeDefinition = new AttributeDefinition { Key = "grand_slam_titles", Label = "Grand Slams" } },
            ],
        };

        await service.GetTriviaBlurbAsync(player);

        using var body = JsonDocument.Parse(handler.LastBody!);
        var prompt = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.Contains("the Men's Tennis player Pete Sampras", prompt);
        Assert.DoesNotContain("Grand Slams", prompt);
        Assert.DoesNotContain("14", prompt);
    }

    [Fact]
    public async Task The_request_asks_for_certain_facts_only_within_200_tokens()
    {
        var handler = new ScriptedHandler(() => Blurb("A blurb."));
        var (service, _) = Create(handler);

        await service.GetTriviaBlurbAsync(PlayerWithId(3));

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(200, body.RootElement.GetProperty("max_tokens").GetInt32());
        var prompt = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.Contains("Only include facts you are certain are accurate.", prompt);
        Assert.Contains("Never correct yourself", prompt);
    }

    [Fact]
    public async Task A_cut_off_answer_is_dropped_logged_and_not_cached()
    {
        var handler = new ScriptedHandler(
            () => Blurb("Katerina Siniakova, the Czech doubles powerhouse, has won multiple Grand Sl", stopReason: "max_tokens"),
            () => Blurb("A finished blurb."));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(3)));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("didn't finish") && e.Message.Contains("max_tokens"));

        // Not cached: the next game on this player asks again.
        Assert.Equal("A finished blurb.", await service.GetTriviaBlurbAsync(PlayerWithId(3)));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task The_blurb_is_read_from_the_first_text_block_even_after_another_kind()
    {
        var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                {"content":[{"type":"thinking","thinking":"Recalling his career..."},{"type":"text","text":"Tim Southee took over 750 international wickets."}],
                 "stop_reason":"end_turn"}
                """, Encoding.UTF8, "application/json")
        });
        var (service, _) = Create(handler);

        Assert.Equal("Tim Southee took over 750 international wickets.", await service.GetTriviaBlurbAsync(PlayerWithId(90)));
    }

    [Fact]
    public async Task An_answer_with_no_text_logs_its_stop_reason_and_block_kinds()
    {
        var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"content":[{"type":"thinking","thinking":"..."}],"stop_reason":"max_tokens"}""",
                Encoding.UTF8, "application/json")
        });
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(90)));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("no text (stop reason max_tokens; blocks: thinking)", warning.Message);
    }

    [Fact]
    public async Task An_empty_answer_logs_that_there_were_no_blocks()
    {
        var handler = new ScriptedHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"content":[],"stop_reason":"refusal"}""", Encoding.UTF8, "application/json")
        });
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(90)));

        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("no text (stop reason refusal; blocks: none)"));
    }

    [Theory]
    [InlineData("She won a doubles title in 2024... Wait, that's inaccurate, so let me correct it: she won it in 2022.")]
    [InlineData("She won Wimbledon in 2019. Correction: it was 2018.")]
    [InlineData("I'm not sure of the year, but she reached a Grand Slam final.")]
    [InlineData("I’m not certain about her partner, but she is a doubles specialist.")]
    [InlineData("I apologise, I don't have reliable information about this player.")]
    public async Task An_answer_that_corrects_itself_or_admits_doubt_is_dropped(string text)
    {
        var handler = new ScriptedHandler(() => Blurb(text));
        var logger = new CapturingLogger<AiTriviaService>();
        var (service, _) = Create(handler, logger);

        Assert.Null(await service.GetTriviaBlurbAsync(PlayerWithId(3)));
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("corrects itself"));
    }

    [Theory]
    [InlineData("She had to wait until 2019 for her first title. Fans waited hours to see her play.")]
    [InlineData("He rose to world No. 1 in 2022. He also won the U.S. Open that year.")]
    public async Task An_ordinary_blurb_passes_the_self_correction_check(string text)
    {
        var (service, _) = Create(new ScriptedHandler(() => Blurb(text)));

        Assert.Equal(text, await service.GetTriviaBlurbAsync(PlayerWithId(3)));
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
