using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

// A missing or non-numeric value for a numeric attribute is bad data. It must
// show as a plain miss (no match, no arrow, not close) and log a warning,
// never fail the whole guess.
public class GameServiceNumericClueTests
{
    private const string Slug = "tennis-men";
    private const string TitlesKey = "career_titles";
    private const int MysteryId = 1;

    private readonly GameDbContext _db;
    private readonly PracticeSessionService _sessions = new();
    private readonly CapturingLogger<GameService> _logger = new();
    private readonly GameService _service;

    public GameServiceNumericClueTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = Slug });
        _db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = TitlesKey, Label = "Titles", Type = AttributeType.Numeric });
        _db.Players.Add(new Player { Id = MysteryId, SportId = 1, Name = "Mystery Player" });
        _db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = MysteryId, AttributeDefinitionId = 1, Value = "10" });
        _db.SaveChanges();

        var settings = new SettingsService(_db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(TimeProvider.System));
        _service = new GameService(
            _db,
            Mock.Of<IAiTriviaService>(),
            new DifficultyService(),
            new NumericClosenessEvaluator(settings),
            new CategoricalClosenessEvaluator(settings),
            _sessions,
            new DailyPuzzleService(_db, NullLogger<DailyPuzzleService>.Instance, TimeProvider.System),
            GameTokenServiceTests.Create(),
            _logger);
    }

    // Adds a guessable player whose titles value is stored as given (null =
    // no row at all), guesses them in practice mode, and returns the clue.
    private async Task<ClueResultDto> GuessPlayerWithTitles(string? titles)
    {
        var player = new Player { Id = 2, SportId = 1, Name = "Guessed Player" };
        _db.Players.Add(player);
        if (titles != null)
            _db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = 1, Value = titles });
        await _db.SaveChangesAsync();

        var sessionId = _sessions.CreateSession(MysteryId);
        var result = await _service.SubmitGuessAsync(Slug, new GuessRequestDto { PlayerId = player.Id, Mode = "easy", SessionId = sessionId });
        return Assert.Single(result.Clues);
    }

    private static void AssertPlainMiss(ClueResultDto clue)
    {
        Assert.False(clue.IsMatch);
        Assert.False(clue.IsClose);
        Assert.Null(clue.Direction);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("twenty")]
    [InlineData("12abc")]
    public async Task InvalidGuessedValue_IsPlainMissAndLogsWarning(string titles)
    {
        var clue = await GuessPlayerWithTitles(titles);

        AssertPlainMiss(clue);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Guessed Player", entry.Message);
        Assert.Contains(TitlesKey, entry.Message);
    }

    [Fact]
    public async Task MissingValueRow_IsPlainMissAndLogsWarning()
    {
        AssertPlainMiss(await GuessPlayerWithTitles(null));
        Assert.Single(_logger.Entries);
    }

    [Fact]
    public async Task InvalidMysteryValue_IsPlainMissAndLogsWarning()
    {
        var mysteryValue = await _db.PlayerAttributeValues.SingleAsync(v => v.PlayerId == MysteryId);
        mysteryValue.Value = "";
        await _db.SaveChangesAsync();

        AssertPlainMiss(await GuessPlayerWithTitles("10"));
        Assert.Contains("Mystery Player", Assert.Single(_logger.Entries).Message);
    }

    [Theory]
    [InlineData("10", true, null)]
    [InlineData("4", false, "up")]
    [InlineData("25", false, "down")]
    public async Task ValidValues_StillCompareNormally(string titles, bool isMatch, string? direction)
    {
        var clue = await GuessPlayerWithTitles(titles);

        Assert.Equal(isMatch, clue.IsMatch);
        Assert.Equal(direction, clue.Direction);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [InlineData("2.5", true)]
    [InlineData(" 7 ", true)]
    [InlineData("-3", true)]
    [InlineData("2,5", false)] // never silently read as 25 or 2.5
    [InlineData("1,000", false)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    public void NumericAttributeValue_ReadsTheSameInEveryLocale(string value, bool valid)
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "de-DE", "fr-FR" })
            {
                Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo(culture);
                Assert.Equal(valid, NumericAttributeValue.TryParse(value, out var number));
                if (value == "2.5")
                    Assert.Equal(2.5m, number);
            }
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
