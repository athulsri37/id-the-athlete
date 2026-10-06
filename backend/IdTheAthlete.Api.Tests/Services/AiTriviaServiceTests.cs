using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    private static (AiTriviaService service, FakeTimeProvider time) Create(ScriptedHandler handler)
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = "tennis-men" });
        db.AppSettings.Add(new AppSetting { Key = "AiTriviaEnabled", Value = "true" });
        db.SaveChanges();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Anthropic:ApiKey"] = "test-key" })
            .Build();
        var time = new FakeTimeProvider(Start);

        return (new AiTriviaService(new HttpClient(handler), config, db, new AiTriviaState(), time), time);
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
}
