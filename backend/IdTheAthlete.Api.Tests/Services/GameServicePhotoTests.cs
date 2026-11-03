using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;

namespace IdTheAthlete.Api.Tests.Services;

// Each guess carries the guessed player's photo, and only a finished game
// carries the answer's, so a photo never gives the mystery player away.
public class GameServicePhotoTests
{
    private const string Slug = "tennis-men";
    private const int MysteryId = 1;
    private const int WithPhotoId = 2;
    private const int WithoutPhotoId = 3;

    private readonly GameService _service;
    private readonly PracticeSessionService _sessions = PracticeSessionServiceTests.Create();

    public GameServicePhotoTests()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = Slug, Family = "tennis" });
        db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = "country", Label = "Country", Type = AttributeType.Categorical });
        db.Players.Add(new Player
        {
            Id = MysteryId, SportId = 1, Name = "Mystery Player",
            PhotoFile = "Mystery 2015.jpg", PhotoCredit = "Jane Doe", PhotoLicense = "CC BY-SA 4.0", PhotoFocusX = 50, PhotoFocusY = 20
        });
        db.Players.Add(new Player
        {
            Id = WithPhotoId, SportId = 1, Name = "Pictured Guess",
            PhotoFile = "Guess.jpg", PhotoCredit = "John Roe", PhotoLicense = "CC BY 2.0", PhotoFocusX = 0, PhotoFocusY = 100
        });
        db.Players.Add(new Player { Id = WithoutPhotoId, SportId = 1, Name = "Unpictured Guess" });
        db.SaveChanges();

        _service = TestGameServiceFactory.Create(db, _sessions);
    }

    private Task<GuessResponseDto> Guess(string sessionId, int playerId) =>
        _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = playerId, Mode = "easy", SessionId = sessionId });

    [Fact]
    public async Task AGuess_CarriesTheGuessedPlayersPhoto_ButNotTheAnswers()
    {
        var sessionId = _sessions.CreateSession(MysteryId, "easy");

        var result = await Guess(sessionId, WithPhotoId);

        var photo = result.GuessedPlayerPhoto!;
        Assert.Equal(("Guess.jpg", "John Roe", "CC BY 2.0", 0, 100), (photo.File, photo.Credit, photo.License, photo.FocusX, photo.FocusY));
        Assert.Null(result.AnswerPhoto);
    }

    [Fact]
    public async Task APlayerWithoutAPhoto_HasNone()
    {
        var sessionId = _sessions.CreateSession(MysteryId, "easy");

        var result = await Guess(sessionId, WithoutPhotoId);

        Assert.Null(result.GuessedPlayerPhoto);
    }

    [Fact]
    public async Task ALostGame_ShowsTheAnswersPhotoOnItsLastGuess()
    {
        var sessionId = _sessions.CreateSession(MysteryId, "easy");
        GuessResponseDto result = null!;
        for (var i = 0; i < GameRules.MaxGuesses; i++)
        {
            result = await Guess(sessionId, WithoutPhotoId);
            if (i < GameRules.MaxGuesses - 1)
                Assert.Null(result.AnswerPhoto);
        }

        Assert.True(result.GameOver);
        Assert.Equal(("Mystery 2015.jpg", 20), (result.AnswerPhoto!.File, result.AnswerPhoto.FocusY));
    }

    [Fact]
    public async Task AWonGame_ShowsTheAnswersPhoto()
    {
        var sessionId = _sessions.CreateSession(MysteryId, "easy");

        var result = await Guess(sessionId, MysteryId);

        Assert.True(result.IsCorrect);
        Assert.Equal("Mystery 2015.jpg", result.AnswerPhoto!.File);
    }
}
