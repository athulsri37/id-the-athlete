using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

// The admin Stats tab's four views, on a hand-worked set of games (see
// StatsScenario), plus the filters and bad input.
public class AdminStatsServiceTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);
    private const string MenAthlete = "Men's Athlete";

    private readonly GameDbContext _db;
    private readonly AdminStatsService _stats;

    public AdminStatsServiceTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = 1, Name = StatsScenario.MenTour, Slug = "tennis-men", Family = "tennis" });
        _db.Sports.Add(new Sport { Id = 2, Name = StatsScenario.WomenTour, Slug = "tennis-women", Family = "tennis" });
        _db.Players.Add(new Player { Id = 10, SportId = 1, Name = MenAthlete });
        _db.Players.Add(new Player { Id = 20, SportId = 2, Name = "Women's Athlete" });
        _db.SaveChanges();
        StatsScenario.Seed(_db, Today, menSportId: 1, womenSportId: 2, menAthleteId: 10, womenAthleteId: 20);

        _stats = new AdminStatsService(_db, new FakeTimeProvider(StatsScenario.Noon(Today)));
    }

    private Task<Dtos.AdminStatsDto> ThisMonth(string? sport = null) =>
        _stats.GetStatsAsync(sport, StatsScenario.FirstOfMonth(Today), Today.ToString("yyyy-MM-dd"));

    [Fact]
    public async Task AllFourViews_MatchTheHandWorkedAnswers()
    {
        StatsScenario.AssertThisMonth(await ThisMonth(), Today, MenAthlete);
    }

    [Fact]
    public async Task OneTour_ShowsOnlyItsGames()
    {
        var stats = await ThisMonth("tennis-women");

        Assert.Equal(new[] { (StatsScenario.WomenTour, "hard") }, stats.Outcomes.Select(r => (r.Tour, r.Mode)));
        Assert.All(stats.WinningGuesses, r => Assert.Equal(StatsScenario.WomenTour, r.Tour));
        Assert.Empty(stats.Puzzles);
    }

    [Fact]
    public async Task NoDates_CoversAllTime()
    {
        var stats = await _stats.GetStatsAsync(null, null, null);

        var menDaily = stats.Outcomes.Single(r => r.Tour == StatsScenario.MenTour && r.Mode == "daily");
        Assert.Equal((6, 3), (menDaily.Games, menDaily.Won));
        Assert.Equal(3, stats.Puzzles.Count);
    }

    [Fact]
    public async Task ARangeWithNoGames_IsEmpty()
    {
        var stats = await _stats.GetStatsAsync(null, "2020-01-01", "2020-01-31");

        Assert.Empty(stats.Outcomes);
        Assert.Empty(stats.WinningGuesses);
        Assert.Empty(stats.Hints);
        Assert.Empty(stats.Puzzles);
    }

    [Fact]
    public async Task ADeletedAthlete_IsShownAsDeleted()
    {
        foreach (var record in _db.GameRecords.Where(r => r.MysteryPlayerId == 10))
            record.MysteryPlayerId = null;
        _db.SaveChanges();

        var stats = await ThisMonth("tennis-men");

        Assert.All(stats.Puzzles, r => Assert.Equal("(deleted)", r.Athlete));
    }

    [Theory]
    [InlineData("13/10/2026", null)]
    [InlineData(null, "2026-13-01")]
    [InlineData("2026-10-13", "2026-10-01")] // from after to
    public async Task BadDates_AreRefused(string? from, string? to)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _stats.GetStatsAsync(null, from, to));
    }

    [Fact]
    public async Task UnknownTour_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _stats.GetStatsAsync("no-such-tour", null, null));
    }
}
