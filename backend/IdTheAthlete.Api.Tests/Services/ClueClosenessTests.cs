using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Tests.TestSupport;

namespace IdTheAthlete.Api.Tests.Services;

// Pins down how every clue is judged (match, close, direction) for each
// sport, end to end through GameService.SubmitGuessAsync, so the rules can
// be reorganised without changing what players see. Settings default to the
// values the AddReferenceData migration creates.
public class ClueClosenessTests
{
    private const string TennisMen = "tennis-men";
    private const string CricketMen = "cricket-men-international";

    private static readonly Dictionary<string, string> SeededSettings = new()
    {
        ["CountryClosenessEnabled"] = "true",
        ["CricketRoleClosenessEnabled"] = "true",
        ["CricketBowlingStyleClosenessEnabled"] = "true",
        ["CricketMatchesClosenessPercent"] = "15",
        ["CricketMatchesClosenessFloor"] = "20",
        ["CricketRunsClosenessPercent"] = "15",
        ["CricketRunsClosenessFloor"] = "500",
        ["CricketWicketsClosenessPercent"] = "15",
        ["CricketWicketsClosenessFloor"] = "15",
    };

    // Builds a one-attribute game for the given sport, guesses the player
    // holding `guessed` while the mystery player holds `mystery`, and
    // returns the single clue.
    private static async Task<ClueResultDto> Judge(
        string sportSlug, string attributeKey, AttributeType type, string guessed, string mystery,
        Dictionary<string, string>? settingOverrides = null)
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Sports.Add(new Sport { Id = 1, Name = sportSlug, Slug = sportSlug, Family = sportSlug.Split('-')[0] });
        db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = attributeKey, Label = attributeKey, Type = type });
        db.Players.Add(new Player { Id = 1, SportId = 1, Name = "Mystery" });
        db.Players.Add(new Player { Id = 2, SportId = 1, Name = "Guess" });
        db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = 1, AttributeDefinitionId = 1, Value = mystery });
        db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = 2, AttributeDefinitionId = 1, Value = guessed });

        var settings = new Dictionary<string, string>(SeededSettings);
        foreach (var (key, value) in settingOverrides ?? new())
            settings[key] = value;
        foreach (var (key, value) in settings)
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        await db.SaveChangesAsync();

        var sessions = PracticeSessionServiceTests.Create();
        var service = TestGameServiceFactory.Create(db, sessions);
        var sessionId = sessions.CreateSession(1, "easy");

        var result = await service.SubmitGuessAsync(sportSlug, new GuessRequestDto { PlayerId = 2, Mode = "easy", SessionId = sessionId });
        return Assert.Single(result.Clues);
    }

    private static void AssertClue(ClueResultDto clue, bool isMatch, bool isClose, string? direction = null)
    {
        Assert.Equal(isMatch, clue.IsMatch);
        Assert.Equal(isClose, clue.IsClose);
        Assert.Equal(direction, clue.Direction);
    }

    // ---- Tennis numeric: fixed absolute thresholds ----

    [Theory]
    [InlineData("grand_slam_titles", "3", "5", true, "up")]      // within 2
    [InlineData("grand_slam_titles", "2", "5", false, "up")]     // 3 away
    [InlineData("career_high_ranking", "15", "10", true, "down")] // within 5
    [InlineData("career_high_ranking", "16", "10", false, "down")]
    [InlineData("turned_pro_year", "2000", "2003", true, "up")]  // within 3
    [InlineData("turned_pro_year", "1999", "2003", false, "up")]
    [InlineData("career_titles", "25", "20", true, "down")]      // within 5
    [InlineData("career_titles", "26", "20", false, "down")]
    public async Task Tennis_Numeric_FixedThresholds(string key, string guessed, string mystery, bool isClose, string direction)
    {
        AssertClue(await Judge(TennisMen, key, AttributeType.Numeric, guessed, mystery), isMatch: false, isClose, direction);
    }

    [Fact]
    public async Task Tennis_Numeric_ExactMatch()
    {
        AssertClue(await Judge(TennisMen, "career_titles", AttributeType.Numeric, "12", "12"), isMatch: true, isClose: false);
    }

    // ---- Tennis categorical: country by land border (+ manual pairs), behind a flag ----

    [Theory]
    [InlineData("France", "Spain", true)]          // land border
    [InlineData("Spain", "France", true)]          // symmetric
    [InlineData("Australia", "New Zealand", true)] // manual override
    [InlineData("India", "China", true)]           // border (not close in cricket)
    [InlineData("France", "Japan", false)]
    public async Task Tennis_Country_LandBorders(string guessed, string mystery, bool isClose)
    {
        AssertClue(await Judge(TennisMen, "country", AttributeType.Categorical, guessed, mystery), isMatch: false, isClose);
    }

    [Fact]
    public async Task Tennis_Country_FlagOff_NeverClose()
    {
        var clue = await Judge(TennisMen, "country", AttributeType.Categorical, "France", "Spain",
            new() { ["CountryClosenessEnabled"] = "false" });
        AssertClue(clue, isMatch: false, isClose: false);
    }

    [Theory]
    [InlineData("plays", "Left", "Right")]
    [InlineData("backhand", "One-Handed", "Two-Handed")]
    [InlineData("active_status", "Active", "Retired")]
    public async Task Tennis_OtherCategorical_NoCloseness(string key, string guessed, string mystery)
    {
        AssertClue(await Judge(TennisMen, key, AttributeType.Categorical, guessed, mystery), isMatch: false, isClose: false);
    }

    [Fact]
    public async Task Categorical_MatchIgnoresCase()
    {
        AssertClue(await Judge(TennisMen, "country", AttributeType.Categorical, "spain", "Spain"), isMatch: true, isClose: false);
    }

    // ---- Cricket numeric: percent of the mystery value, with a floor ----

    [Theory]
    [InlineData("combined_runs", "8500", "10000", true, "up")]   // 15% of 10000 = 1500
    [InlineData("combined_runs", "8499", "10000", false, "up")]
    [InlineData("combined_runs", "1500", "1000", true, "down")]  // floor 500 beats 15% of 1000
    [InlineData("combined_runs", "1501", "1000", false, "down")]
    [InlineData("combined_wickets", "185", "200", true, "up")]   // 15% of 200 = 30
    [InlineData("combined_wickets", "23", "8", true, "down")]    // floor 15
    [InlineData("combined_wickets", "24", "8", false, "down")]
    [InlineData("combined_matches", "80", "100", true, "up")]    // floor 20 beats 15
    [InlineData("combined_matches", "79", "100", false, "up")]
    [InlineData("debut_year", "2010", "2011", false, "up")]      // deliberately no closeness
    public async Task Cricket_Numeric_PercentWithFloor(string key, string guessed, string mystery, bool isClose, string direction)
    {
        AssertClue(await Judge(CricketMen, key, AttributeType.Numeric, guessed, mystery), isMatch: false, isClose, direction);
    }

    [Fact]
    public async Task Cricket_Numeric_SettingMissing_NoCloseness()
    {
        var clue = await Judge(CricketMen, "combined_runs", AttributeType.Numeric, "9999", "10000",
            new() { ["CricketRunsClosenessPercent"] = "not a number" });
        AssertClue(clue, isMatch: false, isClose: false, direction: "up");
    }

    // ---- Cricket categorical ----

    [Theory]
    [InlineData("India", "Pakistan", true)]        // same region, no shared border needed
    [InlineData("Pakistan", "Bangladesh", true)]
    [InlineData("UAE", "Pakistan", true)]
    [InlineData("UAE", "England", false)]
    [InlineData("England", "Netherlands", true)]
    [InlineData("Australia", "New Zealand", true)]
    [InlineData("India", "China", false)]          // land border, but China has no cricket region
    [InlineData("Australia", "India", false)]
    public async Task Cricket_Country_Regions(string guessed, string mystery, bool isClose)
    {
        AssertClue(await Judge(CricketMen, "country", AttributeType.Categorical, guessed, mystery), isMatch: false, isClose);
    }

    [Fact]
    public async Task Cricket_Country_IgnoresTheTennisBorderFlag()
    {
        var clue = await Judge(CricketMen, "country", AttributeType.Categorical, "India", "Pakistan",
            new() { ["CountryClosenessEnabled"] = "false" });
        AssertClue(clue, isMatch: false, isClose: true);
    }

    [Theory]
    [InlineData("Batter", "Wicketkeeper-Batter", true)]
    [InlineData("All-rounder", "Batting All-rounder", true)]
    [InlineData("Bowling All-rounder", "Bowler", true)]
    [InlineData("Batter", "Bowler", false)]
    public async Task Cricket_Role_SharedTags(string guessed, string mystery, bool isClose)
    {
        AssertClue(await Judge(CricketMen, "role", AttributeType.Categorical, guessed, mystery), isMatch: false, isClose);
    }

    [Fact]
    public async Task Cricket_Role_FlagOff_NeverClose()
    {
        var clue = await Judge(CricketMen, "role", AttributeType.Categorical, "Batter", "Wicketkeeper-Batter",
            new() { ["CricketRoleClosenessEnabled"] = "false" });
        AssertClue(clue, isMatch: false, isClose: false);
    }

    [Theory]
    [InlineData("Right-arm Pace", "Left-arm Pace", true)]
    [InlineData("Left-arm Spin", "Right-arm Spin", true)]
    [InlineData("Right-arm Pace", "Right-arm Spin", false)]
    [InlineData("Hasn't Bowled", "Right-arm Spin", false)]
    public async Task Cricket_BowlingStyle_PaceOrSpin(string guessed, string mystery, bool isClose)
    {
        AssertClue(await Judge(CricketMen, "bowling_style", AttributeType.Categorical, guessed, mystery), isMatch: false, isClose);
    }

    [Fact]
    public async Task Cricket_BowlingStyle_FlagOff_NeverClose()
    {
        var clue = await Judge(CricketMen, "bowling_style", AttributeType.Categorical, "Right-arm Pace", "Left-arm Pace",
            new() { ["CricketBowlingStyleClosenessEnabled"] = "false" });
        AssertClue(clue, isMatch: false, isClose: false);
    }

    [Theory]
    [InlineData("batting_hand", "Left", "Right")]
    [InlineData("active_status", "Active", "Retired")]
    public async Task Cricket_OtherCategorical_NoCloseness(string key, string guessed, string mystery)
    {
        AssertClue(await Judge(CricketMen, key, AttributeType.Categorical, guessed, mystery), isMatch: false, isClose: false);
    }
}
