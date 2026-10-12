using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

// The guess count, and with it when the answer and the country hint are
// revealed, must come from server-side state only: the signed token for
// daily games, the session for practice games.
public class GameServiceGuessCountTests
{
    private const string Slug = "tennis-men";
    private const int MysteryId = 1;
    private const int MaxGuesses = 8;
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly GameService _service;
    private readonly GameTokenService _tokens = GameTokenServiceTests.Create();
    private readonly PracticeSessionService _sessions = new();

    public GameServiceGuessCountTests()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = Slug });
        db.Sports.Add(new Sport { Id = 2, Name = "Women's Tennis", Slug = "tennis-women" });
        db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = "country", Label = "Country", Type = AttributeType.Categorical });
        db.Players.Add(new Player { Id = MysteryId, SportId = 1, Name = "Mystery Player" });
        db.Players.Add(new Player { Id = 2, SportId = 1, Name = "Wrong Guess" });
        db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = MysteryId, AttributeDefinitionId = 1, Value = "Spain" });
        db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = 2, AttributeDefinitionId = 1, Value = "France" });
        db.DailyPuzzles.Add(new DailyPuzzle { SportId = 1, PlayerId = MysteryId, PuzzleDate = Today });
        db.SaveChanges();

        var time = new FakeTimeProvider(new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
        var settings = new SettingsService(db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));

        _service = new GameService(
            db,
            Mock.Of<IAiTriviaService>(),
            new DifficultyService(),
            new NumericClosenessEvaluator(settings),
            new CategoricalClosenessEvaluator(settings),
            _sessions,
            new DailyPuzzleService(db, NullLogger<DailyPuzzleService>.Instance, time),
            _tokens);
    }

    private Task<GuessResponseDto> DailyWrongGuess(string? token) =>
        _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = 2, Mode = "daily", GameToken = token });

    private async Task<string> DailyTokenAfter(int wrongGuesses)
    {
        string? token = null;
        for (var i = 0; i < wrongGuesses; i++)
            token = (await DailyWrongGuess(token)).GameToken;
        return token!;
    }

    [Fact]
    public async Task Daily_FirstGuessWithoutToken_IsGuessOneAndHidesAnswer()
    {
        var result = await DailyWrongGuess(null);

        Assert.False(result.GameOver);
        Assert.Null(result.AnswerName);
        Assert.Equal(new DailyGameState(1, Today, 1), _tokens.Read(result.GameToken!));
    }

    [Fact]
    public async Task Daily_EighthRealGuess_RevealsAnswer()
    {
        var token = await DailyTokenAfter(MaxGuesses - 1);

        var result = await DailyWrongGuess(token);

        Assert.True(result.GameOver);
        Assert.Equal("Mystery Player", result.AnswerName);
    }

    [Fact]
    public async Task Daily_ReusingAnEarlyToken_NeverAdvancesTheCount()
    {
        var firstToken = await DailyTokenAfter(1);

        for (var i = 0; i < MaxGuesses; i++)
        {
            var result = await DailyWrongGuess(firstToken);
            Assert.Null(result.AnswerName);
        }
    }

    [Fact]
    public async Task Daily_GuessAfterGameOver_Throws()
    {
        var token = await DailyTokenAfter(MaxGuesses);

        await Assert.ThrowsAsync<InvalidOperationException>(() => DailyWrongGuess(token));
    }

    [Fact]
    public async Task Daily_ForgedToken_Throws()
    {
        var forged = GameTokenServiceTests.Create(key: "attacker-chosen-key-that-is-long-enough")
            .Issue(new DailyGameState(1, Today, MaxGuesses - 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => DailyWrongGuess(forged));
    }

    [Fact]
    public async Task Daily_TokenFromAnotherSport_Throws()
    {
        var otherSportToken = _tokens.Issue(new DailyGameState(2, Today, 3));

        await Assert.ThrowsAsync<InvalidOperationException>(() => DailyWrongGuess(otherSportToken));
    }

    [Fact]
    public async Task Daily_HintWithoutToken_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetCountryHintAsync(Slug, "daily", null));
    }

    [Fact]
    public async Task Daily_HintAfterFourGuesses_Throws()
    {
        var token = await DailyTokenAfter(4);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetCountryHintAsync(Slug, "daily", null, gameToken: token));
    }

    [Fact]
    public async Task Daily_HintAfterFiveGuesses_ReturnsCountry()
    {
        var token = await DailyTokenAfter(5);

        Assert.Equal("Spain", await _service.GetCountryHintAsync(Slug, "daily", null, gameToken: token));
    }

    [Fact]
    public async Task Practice_AnswerOnlyRevealedOnEighthGuess()
    {
        var sessionId = _sessions.CreateSession(MysteryId);
        var request = new GuessRequestDto { PlayerId = 2, Mode = "easy", SessionId = sessionId };

        for (var i = 1; i < MaxGuesses; i++)
            Assert.Null((await _service.SubmitGuessAsync(Slug, request)).AnswerName);

        Assert.Equal("Mystery Player", (await _service.SubmitGuessAsync(Slug, request)).AnswerName);
    }

    [Fact]
    public async Task Practice_HintBeforeFiveGuesses_Throws()
    {
        var sessionId = _sessions.CreateSession(MysteryId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetCountryHintAsync(Slug, "easy", sessionId));
    }

    [Fact]
    public async Task Practice_InvalidGuess_DoesNotUseUpAGuess()
    {
        var sessionId = _sessions.CreateSession(MysteryId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = 999, Mode = "easy", SessionId = sessionId }));

        Assert.Equal(0, _sessions.GetGuessCount(sessionId));
    }
}
