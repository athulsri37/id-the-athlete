using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using IdTheAthlete.Api.Tests.TestSupport;

namespace IdTheAthlete.Api.Tests.Services;

// Trivia is fetched separately from the guess, so the final guess never
// waits on the AI call. The game-over response carries a trivia token, and
// only that token unlocks the mystery player's trivia.
public class GameServiceTriviaTests
{
    private const string Slug = "tennis-men";
    private const int MysteryId = 1;
    private const int MaxGuesses = 8;

    private readonly GameService _service;
    private readonly GameTokenService _tokens = GameTokenServiceTests.Create();
    private readonly PracticeSessionService _sessions = new();
    private readonly Mock<IAiTriviaService> _trivia = new();

    public GameServiceTriviaTests()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = Slug, Family = "tennis" });
        db.Sports.Add(new Sport { Id = 2, Name = "Women's Tennis", Slug = "tennis-women", Family = "tennis" });
        db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = "country", Label = "Country", Type = AttributeType.Categorical });
        db.Players.Add(new Player { Id = MysteryId, SportId = 1, Name = "Mystery Player" });
        db.Players.Add(new Player { Id = 2, SportId = 1, Name = "Wrong Guess" });
        db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = MysteryId, AttributeDefinitionId = 1, Value = "Spain" });
        db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = 2, AttributeDefinitionId = 1, Value = "France" });
        db.SaveChanges();

        _trivia.Setup(t => t.GetTriviaBlurbAsync(It.IsAny<Player>())).ReturnsAsync("A fun fact.");
        _service = TestGameServiceFactory.Create(db, _sessions, _tokens, trivia: _trivia.Object);
    }

    private Task<GuessResponseDto> PracticeGuess(string sessionId, int playerId) =>
        _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = playerId, Mode = "easy", SessionId = sessionId });

    [Fact]
    public async Task Guess_NeverCallsTheAi_AndOnlyGameOverCarriesAToken()
    {
        var sessionId = _sessions.CreateSession(MysteryId);

        var wrong = await PracticeGuess(sessionId, 2);
        var correct = await PracticeGuess(sessionId, MysteryId);

        Assert.Null(wrong.TriviaToken);
        Assert.Equal(new TriviaGrant(1, MysteryId), _tokens.ReadTrivia(correct.TriviaToken!));
        _trivia.Verify(t => t.GetTriviaBlurbAsync(It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public async Task Guess_LostGame_CarriesTokenForTheMysteryPlayer()
    {
        var sessionId = _sessions.CreateSession(MysteryId);

        GuessResponseDto last = null!;
        for (var i = 0; i < MaxGuesses; i++)
            last = await PracticeGuess(sessionId, 2);

        Assert.True(last.GameOver);
        Assert.Equal(new TriviaGrant(1, MysteryId), _tokens.ReadTrivia(last.TriviaToken!));
    }

    [Fact]
    public async Task Trivia_ValidToken_ReturnsBlurbForThatPlayer()
    {
        var token = _tokens.IssueTrivia(new TriviaGrant(1, MysteryId));

        Assert.Equal("A fun fact.", await _service.GetTriviaAsync(Slug, token));
        _trivia.Verify(t => t.GetTriviaBlurbAsync(It.Is<Player>(p => p.Id == MysteryId)), Times.Once);
    }

    [Fact]
    public async Task Trivia_Unavailable_ReturnsNull()
    {
        _trivia.Setup(t => t.GetTriviaBlurbAsync(It.IsAny<Player>())).ReturnsAsync((string?)null);

        Assert.Null(await _service.GetTriviaAsync(Slug, _tokens.IssueTrivia(new TriviaGrant(1, MysteryId))));
    }

    [Fact]
    public async Task Trivia_TokenFromAnotherSport_Throws()
    {
        var token = _tokens.IssueTrivia(new TriviaGrant(2, MysteryId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetTriviaAsync(Slug, token));
    }

    [Fact]
    public async Task Trivia_DailyGameToken_Throws()
    {
        var token = _tokens.Issue(new DailyGameState(1, new DateOnly(2026, 10, 12), MaxGuesses));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetTriviaAsync(Slug, token));
    }

    [Fact]
    public async Task Trivia_ForgedToken_Throws()
    {
        var forged = GameTokenServiceTests.Create(key: "attacker-chosen-key-that-is-long-enough")
            .IssueTrivia(new TriviaGrant(1, MysteryId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetTriviaAsync(Slug, forged));
    }
}
