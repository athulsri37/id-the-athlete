using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

// Playing real games through GameService leaves one stats row per game,
// counted from the first guess, with what the server knows (never what a
// request claims).
public class GameServiceStatsTests
{
    private const string Slug = "tennis-men";
    private const int MysteryId = 1;
    private const int WrongId = 2;
    private static readonly DateOnly Today = new(2026, 10, 13);

    private readonly GameDbContext _db;
    private readonly GameService _service;
    private readonly GameTokenService _tokens = GameTokenServiceTests.Create();
    private readonly PracticeSessionService _sessions = PracticeSessionServiceTests.Create();

    public GameServiceStatsTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = Slug, Family = "tennis" });
        _db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = "country", Label = "Country", Type = AttributeType.Categorical });
        _db.Players.Add(new Player { Id = MysteryId, SportId = 1, Name = "Mystery Player" });
        _db.Players.Add(new Player { Id = WrongId, SportId = 1, Name = "Wrong Guess" });
        _db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = MysteryId, AttributeDefinitionId = 1, Value = "Spain" });
        _db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = WrongId, AttributeDefinitionId = 1, Value = "France" });
        _db.DailyPuzzles.Add(new DailyPuzzle { SportId = 1, PlayerId = MysteryId, PuzzleDate = Today });
        _db.SaveChanges();

        var time = new FakeTimeProvider(new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
        _service = TestGameServiceFactory.Create(_db, _sessions, _tokens, time: time);
    }

    private async Task<string> DailyGuesses(int count, string? token = null, int playerId = WrongId)
    {
        for (var i = 0; i < count; i++)
            token = (await _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = playerId, Mode = "daily", GameToken = token })).GameToken;
        return token!;
    }

    private List<GameRecord> Records() => _db.GameRecords.AsNoTracking().ToList();

    [Fact]
    public async Task DailyGame_IsOneRowUnderTheTokensGameId()
    {
        var token = await DailyGuesses(3);

        var record = Assert.Single(Records());
        Assert.Equal(_tokens.Read(token).GameId, record.Id);
        Assert.Equal(("daily", Today, MysteryId, 3), (record.Mode, record.PuzzleDate, record.MysteryPlayerId, record.GuessCount));
        Assert.Null(record.Result);
    }

    [Fact]
    public async Task DailyGame_LostWithTheHintTaken()
    {
        var token = await DailyGuesses(5);
        await _service.GetCountryHintAsync(Slug, "daily", null, gameToken: token);
        await DailyGuesses(3, token);

        var record = Assert.Single(Records());
        Assert.Equal((GameResult.Lost, 8, 5), (record.Result, record.GuessCount, record.HintTakenAtGuess));
        Assert.NotNull(record.EndedAt);
    }

    [Fact]
    public async Task DailyGame_Won()
    {
        var token = await DailyGuesses(2);
        await DailyGuesses(1, token, playerId: MysteryId);

        var record = Assert.Single(Records());
        Assert.Equal((GameResult.Won, 3), (record.Result, record.GuessCount));
    }

    [Fact]
    public async Task EachNewDailyGame_IsItsOwnRow()
    {
        await DailyGuesses(2);
        await DailyGuesses(1);

        Assert.Equal(2, Records().Count);
    }

    [Fact]
    public async Task PracticeGameWithNoGuesses_IsNotRecorded()
    {
        // "hard" draws from every tier, so these stat-less players qualify.
        await _service.StartPracticeGameAsync(Slug, "hard");

        Assert.Empty(Records());
    }

    [Fact]
    public async Task PracticeGame_RecordsTheDifficultyItStartedWith_NotTheRequestsMode()
    {
        // The tier rules would never put this player in "hard" only, so
        // start the session directly as the service would for "hard".
        var sessionId = _sessions.CreateSession(MysteryId, "hard");

        await _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = WrongId, Mode = "easy", SessionId = sessionId });
        await _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = MysteryId, Mode = "easy", SessionId = sessionId });

        var record = Assert.Single(Records());
        Assert.Equal(("hard", (DateOnly?)null, 2, GameResult.Won), (record.Mode, record.PuzzleDate, record.GuessCount, record.Result));
    }

    [Fact]
    public async Task PracticeGame_HintIsRecorded()
    {
        var sessionId = _sessions.CreateSession(MysteryId, "medium");
        var request = new GuessRequestDto { PlayerId = WrongId, Mode = "medium", SessionId = sessionId };
        for (var i = 0; i < 6; i++)
            await _service.SubmitGuessAsync(Slug, request);

        await _service.GetCountryHintAsync(Slug, "medium", sessionId);

        Assert.Equal(6, Assert.Single(Records()).HintTakenAtGuess);
    }

    [Fact]
    public async Task RefusedGuess_IsNotRecorded()
    {
        var sessionId = _sessions.CreateSession(MysteryId, "easy");

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = 999, Mode = "easy", SessionId = sessionId }));

        Assert.Empty(Records());
    }
}
