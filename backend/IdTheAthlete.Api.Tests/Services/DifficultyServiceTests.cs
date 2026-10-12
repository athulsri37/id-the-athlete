using System.Globalization;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Tests.Services;

public class DifficultyServiceTests
{
    private readonly DifficultyService _service = new();

    private static Player PlayerWith(params (string Key, string Value)[] stats) => new()
    {
        Name = "Test Player",
        AttributeValues = stats
            .Select(s => new PlayerAttributeValue { Value = s.Value, AttributeDefinition = new AttributeDefinition { Key = s.Key } })
            .ToList(),
    };

    [Theory]
    [InlineData("1", "0", "0", "easy")]   // reached #1
    [InlineData("50", "20", "0", "easy")] // 20+ titles
    [InlineData("50", "0", "2", "easy")]  // 2+ Grand Slams
    [InlineData("10", "19", "1", "medium")]
    [InlineData("10", "5", "0", "medium")]
    [InlineData("10", "4", "1", "hard")]  // one Slam alone isn't enough
    public void Tennis_TierBoundaries(string highRank, string titles, string slams, string expected)
    {
        var player = PlayerWith(("career_high_ranking", highRank), ("career_titles", titles), ("grand_slam_titles", slams));

        Assert.Equal(expected, _service.ComputeDifficultyTier(player, "tennis-men"));
        Assert.Equal(expected, _service.ComputeDifficultyTier(player, "tennis-women"));
    }

    [Theory]
    [InlineData("10000", "0", "0", "easy")]
    [InlineData("0", "300", "0", "easy")]
    [InlineData("0", "0", "300", "easy")]
    [InlineData("3000", "0", "0", "medium")]
    [InlineData("9999", "299", "299", "medium")]
    [InlineData("2999", "99", "149", "hard")]
    public void CricketMen_TierBoundaries(string runs, string wickets, string matches, string expected)
    {
        var player = PlayerWith(("combined_runs", runs), ("combined_wickets", wickets), ("combined_matches", matches));

        Assert.Equal(expected, _service.ComputeDifficultyTier(player, "cricket-men-international"));
    }

    [Theory]
    [InlineData("8000", "0", "0", "easy")]
    [InlineData("0", "250", "0", "easy")]
    [InlineData("0", "0", "250", "easy")]
    [InlineData("2500", "0", "0", "medium")]
    [InlineData("2499", "89", "129", "hard")]
    public void CricketWomen_TierBoundaries(string runs, string wickets, string matches, string expected)
    {
        var player = PlayerWith(("combined_runs", runs), ("combined_wickets", wickets), ("combined_matches", matches));

        Assert.Equal(expected, _service.ComputeDifficultyTier(player, "cricket-women-international"));
    }

    [Fact]
    public void Override_WinsOverComputedTier()
    {
        var player = PlayerWith(("career_titles", "40"));
        player.IsOverridden = true;
        player.DifficultyOverride = "Hard";

        Assert.Equal("hard", _service.ComputeDifficultyTier(player, "tennis-men"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,000")]
    public void InvalidValue_CountsAsZero(string titles)
    {
        var player = PlayerWith(("career_high_ranking", "10"), ("career_titles", titles));

        Assert.Equal("hard", _service.ComputeDifficultyTier(player, "tennis-men"));
    }

    [Fact]
    public void Values_ReadTheSameInEveryLocale()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var player = PlayerWith(("combined_runs", " 10000 "));

            Assert.Equal("easy", _service.ComputeDifficultyTier(player, "cricket-men-international"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
