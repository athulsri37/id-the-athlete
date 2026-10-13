using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

// The daily puzzle never repeats an athlete used in the previous 13 days
// (a puzzle exactly 14 days back is fair game again), picks only from its
// own sport, and falls back to the whole roster rather than failing when
// every athlete was used recently.
public class DailyPuzzleServiceTests
{
    private const int SportId = 1;
    private const int OtherSportId = 2;
    private static readonly DateOnly Date = new(2026, 10, 13);

    private readonly GameDbContext _db;
    private readonly DailyPuzzleService _service;

    public DailyPuzzleServiceTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = SportId, Name = "Men's Tennis", Slug = "tennis-men", Family = "tennis" });
        _db.Sports.Add(new Sport { Id = OtherSportId, Name = "Women's Tennis", Slug = "tennis-women", Family = "tennis" });
        _db.SaveChanges();

        var time = new FakeTimeProvider(new DateTimeOffset(Date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
        _service = new DailyPuzzleService(_db, NullLogger<DailyPuzzleService>.Instance, time);
    }

    private void AddPlayers(int sportId, params int[] ids)
    {
        foreach (var id in ids)
            _db.Players.Add(new Player { Id = id, SportId = sportId, Name = $"Player {id}" });
        _db.SaveChanges();
    }

    private void AddPuzzle(int playerId, int daysAgo)
    {
        _db.DailyPuzzles.Add(new DailyPuzzle { SportId = SportId, PlayerId = playerId, PuzzleDate = Date.AddDays(-daysAgo) });
        _db.SaveChanges();
    }

    private int PuzzlePlayerOn(DateOnly date) =>
        _db.DailyPuzzles.AsNoTracking().Single(d => d.SportId == SportId && d.PuzzleDate == date).PlayerId;

    [Fact]
    public async Task AnAthleteFromTheLast13Days_IsNeverPicked()
    {
        // Players 1-13 were the answers 1-13 days ago; only player 14 is free.
        AddPlayers(SportId, Enumerable.Range(1, 14).ToArray());
        for (var daysAgo = 1; daysAgo <= 13; daysAgo++)
            AddPuzzle(playerId: daysAgo, daysAgo);

        Assert.True(await _service.EnsureDailyPuzzleAsync(SportId, Date));

        Assert.Equal(14, PuzzlePlayerOn(Date));
    }

    [Fact]
    public async Task AnAthleteFrom14DaysAgo_CanBePickedAgain()
    {
        // Player 1 was 14 days ago, players 2-14 cover the 13 days since:
        // player 1 is the only one allowed.
        AddPlayers(SportId, Enumerable.Range(1, 14).ToArray());
        AddPuzzle(playerId: 1, daysAgo: 14);
        for (var daysAgo = 1; daysAgo <= 13; daysAgo++)
            AddPuzzle(playerId: daysAgo + 1, daysAgo);

        Assert.True(await _service.EnsureDailyPuzzleAsync(SportId, Date));

        Assert.Equal(1, PuzzlePlayerOn(Date));
    }

    [Fact]
    public async Task OnlyTheSportsOwnAthletes_ArePicked()
    {
        AddPlayers(SportId, 1);
        AddPlayers(OtherSportId, 2, 3, 4);

        for (var i = 0; i < 5; i++)
            Assert.True(await _service.EnsureDailyPuzzleAsync(SportId, Date.AddDays(i)));

        Assert.All(_db.DailyPuzzles.AsNoTracking().Where(d => d.SportId == SportId), d => Assert.Equal(1, d.PlayerId));
    }

    [Fact]
    public async Task EveryAthleteUsedRecently_FallsBackToTheWholeRoster()
    {
        AddPlayers(SportId, 1, 2);
        AddPuzzle(playerId: 1, daysAgo: 1);
        AddPuzzle(playerId: 2, daysAgo: 2);

        Assert.True(await _service.EnsureDailyPuzzleAsync(SportId, Date));

        Assert.Contains(PuzzlePlayerOn(Date), new[] { 1, 2 });
    }

    [Fact]
    public async Task AnExistingPuzzle_IsKept()
    {
        AddPlayers(SportId, 1, 2);
        _db.DailyPuzzles.Add(new DailyPuzzle { SportId = SportId, PlayerId = 2, PuzzleDate = Date });
        _db.SaveChanges();

        Assert.False(await _service.EnsureDailyPuzzleAsync(SportId, Date));

        Assert.Equal(2, PuzzlePlayerOn(Date));
    }
}
