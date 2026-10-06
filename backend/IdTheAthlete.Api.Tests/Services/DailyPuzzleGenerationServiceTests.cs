using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

public class DailyPuzzleGenerationServiceTests
{
    [Fact]
    public async Task Generates_the_next_days_puzzle_when_the_clock_passes_midnight_utc()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2030, 1, 15, 23, 0, 0, TimeSpan.Zero));
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection()
            .AddDbContext<GameDbContext>(o => o.UseInMemoryDatabase(dbName))
            .AddScoped<IDailyPuzzleService, DailyPuzzleService>()
            .AddSingleton<TimeProvider>(time)
            .AddLogging()
            .BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = "tennis-men" });
            for (var i = 1; i <= 20; i++)
                db.Players.Add(new Player { Id = i, SportId = 1, Name = $"Player {i}" });
            db.SaveChanges();
        }

        List<DateOnly> PuzzleDates()
        {
            using var scope = services.CreateScope();
            return scope.ServiceProvider.GetRequiredService<GameDbContext>()
                .DailyPuzzles.Select(p => p.PuzzleDate).OrderBy(d => d).ToList();
        }

        var job = new DailyPuzzleGenerationService(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DailyPuzzleGenerationService>.Instance,
            time);
        await job.StartAsync(CancellationToken.None);

        // Startup catch-up creates today's puzzle (fake "today" is 2030-01-15).
        await WaitUntil(() => PuzzleDates().Count == 1);
        Assert.Equal(new[] { new DateOnly(2030, 1, 15) }, PuzzleDates());

        // Move the fake clock forward a minute at a time; the job's scheduled
        // run fires at 00:00 UTC and creates the 2030-01-16 puzzle.
        for (var minutes = 0; minutes < 180 && PuzzleDates().Count < 2; minutes++)
        {
            time.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(5);
        }

        await job.StopAsync(CancellationToken.None);
        Assert.Equal(new[] { new DateOnly(2030, 1, 15), new DateOnly(2030, 1, 16) }, PuzzleDates());
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
    }
}
