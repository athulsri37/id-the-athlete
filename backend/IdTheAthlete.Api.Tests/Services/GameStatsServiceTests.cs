using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

// A game's stats row is created by its first guess, updated by later
// guesses and the hint, and a failure never reaches the game.
public class GameStatsServiceTests
{
    private const int SportId = 1;
    private const int PlayerId = 7;
    private static readonly DateTimeOffset Noon = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Noon.UtcDateTime);

    private readonly GameDbContext _db;
    private readonly FakeTimeProvider _time = new(Noon);
    private readonly GameStatsService _stats;

    public GameStatsServiceTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = SportId, Name = "Men's Tennis", Slug = "tennis-men", Family = "tennis" });
        _db.Players.Add(new Player { Id = PlayerId, SportId = SportId, Name = "Mystery Player" });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        _stats = new GameStatsService(_db, _time, NullLogger<GameStatsService>.Instance);
    }

    private static GameGuessStat Guess(Guid gameId, int number, bool gameOver = false, bool won = false,
        string mode = "daily", DateOnly? puzzleDate = null) =>
        new(gameId, SportId, mode, mode == "daily" ? puzzleDate ?? Today : null, PlayerId, number, gameOver, won);

    private GameRecord Stored(Guid gameId) => _db.GameRecords.AsNoTracking().Single(r => r.Id == gameId);

    [Fact]
    public async Task FirstGuess_CreatesTheGamesRow()
    {
        var gameId = Guid.NewGuid();

        await _stats.RecordGuessAsync(Guess(gameId, 1));

        var record = Stored(gameId);
        Assert.Equal((SportId, "daily", Today, PlayerId, 1), (record.SportId, record.Mode, record.PuzzleDate, record.MysteryPlayerId, record.GuessCount));
        Assert.Equal(Noon.UtcDateTime, record.StartedAt);
        Assert.Equal(Noon.UtcDateTime, record.LastActivityAt);
        Assert.Null(record.Result);
        Assert.Null(record.EndedAt);
        Assert.Null(record.HintTakenAtGuess);
        Assert.False(record.IsPastChallenge);
    }

    [Fact]
    public async Task PracticeGame_HasNoPuzzleDate()
    {
        var gameId = Guid.NewGuid();

        await _stats.RecordGuessAsync(Guess(gameId, 1, mode: "hard"));

        var record = Stored(gameId);
        Assert.Equal("hard", record.Mode);
        Assert.Null(record.PuzzleDate);
        Assert.False(record.IsPastChallenge);
    }

    [Fact]
    public async Task DailyGameOnAnEarlierPuzzle_IsAPastChallenge()
    {
        var gameId = Guid.NewGuid();

        await _stats.RecordGuessAsync(Guess(gameId, 1, puzzleDate: Today.AddDays(-3)));

        Assert.True(Stored(gameId).IsPastChallenge);
    }

    [Fact]
    public async Task LaterGuesses_UpdateCountAndActivity_KeepStart()
    {
        var gameId = Guid.NewGuid();
        await _stats.RecordGuessAsync(Guess(gameId, 1));

        _time.Advance(TimeSpan.FromMinutes(3));
        await _stats.RecordGuessAsync(Guess(gameId, 2));
        _time.Advance(TimeSpan.FromMinutes(4));
        await _stats.RecordGuessAsync(Guess(gameId, 3));

        var record = Stored(gameId);
        Assert.Equal(3, record.GuessCount);
        Assert.Equal(Noon.UtcDateTime, record.StartedAt);
        Assert.Equal(Noon.AddMinutes(7).UtcDateTime, record.LastActivityAt);
        Assert.Null(record.Result);
        Assert.Equal("daily", record.Mode);
    }

    [Theory]
    [InlineData(true, GameResult.Won)]
    [InlineData(false, GameResult.Lost)]
    public async Task FinalGuess_RecordsResultAndEnd(bool won, GameResult expected)
    {
        var gameId = Guid.NewGuid();
        await _stats.RecordGuessAsync(Guess(gameId, 1));
        _time.Advance(TimeSpan.FromMinutes(5));

        await _stats.RecordGuessAsync(Guess(gameId, 8, gameOver: true, won: won));

        var record = Stored(gameId);
        Assert.Equal(expected, record.Result);
        Assert.Equal(8, record.GuessCount);
        Assert.Equal(Noon.AddMinutes(5).UtcDateTime, record.EndedAt);
    }

    [Fact]
    public async Task WinOnTheFirstGuess_IsCreatedFinished()
    {
        var gameId = Guid.NewGuid();

        await _stats.RecordGuessAsync(Guess(gameId, 1, gameOver: true, won: true));

        var record = Stored(gameId);
        Assert.Equal(GameResult.Won, record.Result);
        Assert.Equal(record.StartedAt, record.EndedAt);
    }

    [Fact]
    public async Task GameWithoutAnId_IsNotRecorded()
    {
        await _stats.RecordGuessAsync(Guess(Guid.Empty, 1));
        await _stats.RecordHintAsync(Guid.Empty, 5);

        Assert.Empty(_db.GameRecords);
    }

    [Fact]
    public async Task LaterGuessForAnUnrecordedGame_IsSkippedQuietly()
    {
        // e.g. a daily game that started before a deploy added stats.
        await _stats.RecordGuessAsync(Guess(Guid.NewGuid(), 4));

        Assert.Empty(_db.GameRecords);
        Assert.Empty(_db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Hint_RecordsTheGuessItWasTakenAt_OnlyTheFirstTime()
    {
        var gameId = Guid.NewGuid();
        await _stats.RecordGuessAsync(Guess(gameId, 1));
        _time.Advance(TimeSpan.FromMinutes(2));

        await _stats.RecordHintAsync(gameId, 5);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _stats.RecordHintAsync(gameId, 6);

        var record = Stored(gameId);
        Assert.Equal(5, record.HintTakenAtGuess);
        Assert.Equal(Noon.AddMinutes(3).UtcDateTime, record.LastActivityAt);
    }

    [Fact]
    public async Task HintForAnUnrecordedGame_IsSkippedQuietly()
    {
        await _stats.RecordHintAsync(Guid.NewGuid(), 5);

        Assert.Empty(_db.GameRecords);
    }

    [Fact]
    public async Task UnreachableDatabase_NeverReachesTheGame_AndLogsAWarning()
    {
        var unreachable = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=x;Password=x;Timeout=3")
            .Options);
        var logger = new CapturingLogger<GameStatsService>();
        var stats = new GameStatsService(unreachable, _time, logger);

        await stats.RecordGuessAsync(Guess(Guid.NewGuid(), 1));
        await stats.RecordGuessAsync(Guess(Guid.NewGuid(), 2));
        await stats.RecordHintAsync(Guid.NewGuid(), 5);

        Assert.Equal(3, logger.Entries.Count(e => e.Level == LogLevel.Warning));
        Assert.Empty(unreachable.ChangeTracker.Entries());
    }
}
