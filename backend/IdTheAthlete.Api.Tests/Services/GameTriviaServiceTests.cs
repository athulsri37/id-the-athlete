using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

// Only the trivia token from a game-over guess unlocks a player's trivia.
public class GameTriviaServiceTests
{
    private const string Slug = "tennis-men";
    private const int PlayerId = 1;

    private readonly GameTriviaService _service;
    private readonly GameTokenService _tokens = GameTokenServiceTests.Create();
    private readonly Mock<IAiTriviaService> _trivia = new();

    public GameTriviaServiceTests()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = Slug, Family = "tennis" });
        db.Sports.Add(new Sport { Id = 2, Name = "Women's Tennis", Slug = "tennis-women", Family = "tennis" });
        db.Players.Add(new Player { Id = PlayerId, SportId = 1, Name = "Mystery Player" });
        db.SaveChanges();

        _trivia.Setup(t => t.GetTriviaBlurbAsync(It.IsAny<Player>())).ReturnsAsync("A fun fact.");
        _service = new GameTriviaService(db, _tokens, _trivia.Object);
    }

    [Fact]
    public async Task ValidToken_ReturnsBlurbForThatPlayer()
    {
        var token = _tokens.IssueTrivia(new TriviaGrant(1, PlayerId));

        Assert.Equal("A fun fact.", await _service.GetTriviaAsync(Slug, token));
        _trivia.Verify(t => t.GetTriviaBlurbAsync(It.Is<Player>(p => p.Id == PlayerId)), Times.Once);
    }

    [Fact]
    public async Task TriviaUnavailable_ReturnsNull()
    {
        _trivia.Setup(t => t.GetTriviaBlurbAsync(It.IsAny<Player>())).ReturnsAsync((string?)null);

        Assert.Null(await _service.GetTriviaAsync(Slug, _tokens.IssueTrivia(new TriviaGrant(1, PlayerId))));
    }

    [Fact]
    public async Task TokenFromAnotherSport_Throws()
    {
        var token = _tokens.IssueTrivia(new TriviaGrant(2, PlayerId));

        await Assert.ThrowsAsync<InvalidRequestException>(() => _service.GetTriviaAsync(Slug, token));
        _trivia.Verify(t => t.GetTriviaBlurbAsync(It.IsAny<Player>()), Times.Never);
    }

    [Fact]
    public async Task DailyGameToken_Throws()
    {
        var token = _tokens.Issue(new DailyGameState(1, new DateOnly(2026, 10, 12), 8));

        await Assert.ThrowsAsync<InvalidRequestException>(() => _service.GetTriviaAsync(Slug, token));
    }

    [Fact]
    public async Task ForgedToken_Throws()
    {
        var forged = GameTokenServiceTests.Create(key: "attacker-chosen-key-that-is-long-enough")
            .IssueTrivia(new TriviaGrant(1, PlayerId));

        await Assert.ThrowsAsync<InvalidRequestException>(() => _service.GetTriviaAsync(Slug, forged));
    }

    [Fact]
    public async Task UnknownSport_Throws()
    {
        var token = _tokens.IssueTrivia(new TriviaGrant(1, PlayerId));

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetTriviaAsync("no-such-sport", token));
    }
}
