using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Rules;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

public class SportRulesRegistryTests
{
    private readonly SportRulesRegistry _registry = TestGameServiceFactory.CreateRegistry(Mock.Of<ISettingsService>());

    private static Sport Sport(string slug, string family) => new() { Slug = slug, Family = family };

    [Theory]
    [InlineData("tennis-men", "tennis", typeof(TennisRules))]
    [InlineData("tennis-women", "tennis", typeof(TennisRules))]
    [InlineData("cricket-men-international", "cricket", typeof(CricketRules))]
    [InlineData("cricket-women-international", "cricket", typeof(CricketRules))]
    [InlineData("tennis-men", "TENNIS", typeof(TennisRules))] // family match ignores case
    public void For_SeededSports_ReturnsTheirFamilyRules(string slug, string family, Type expected)
    {
        Assert.IsType(expected, _registry.For(Sport(slug, family)));
    }

    [Fact]
    public void For_UnknownFamily_Throws_InsteadOfFallingBackToTennis()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _registry.For(Sport("football-premier-league", "football")));

        Assert.Contains("football-premier-league", error.Message);
        Assert.Contains("known families: cricket, tennis", error.Message);
    }

    [Fact]
    public void For_EmptyFamily_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _registry.For(Sport("tennis-men", "")));
    }

    [Fact]
    public void For_CricketTourWithoutDifficultyThresholds_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _registry.For(Sport("cricket-men-domestic", "cricket")));

        Assert.Contains("CricketRules doesn't support it", error.Message);
    }

    [Fact]
    public void EnsureEverySportIsSupported_AllSeededSports_DoesNotThrow()
    {
        _registry.EnsureEverySportIsSupported(new[]
        {
            Sport("tennis-men", "tennis"),
            Sport("tennis-women", "tennis"),
            Sport("cricket-men-international", "cricket"),
            Sport("cricket-women-international", "cricket"),
        });
    }

    [Fact]
    public void EnsureEverySportIsSupported_NamesEveryProblemSport()
    {
        var error = Assert.Throws<InvalidOperationException>(() => _registry.EnsureEverySportIsSupported(new[]
        {
            Sport("tennis-men", "tennis"),
            Sport("football-premier-league", "football"),
            Sport("cricket-men-domestic", "cricket"),
        }));

        Assert.Contains("football-premier-league", error.Message);
        Assert.Contains("cricket-men-domestic", error.Message);
        Assert.DoesNotContain("'tennis-men'", error.Message);
    }

    [Fact]
    public void Constructor_TwoRulesForTheSameFamily_Throws()
    {
        var settings = Mock.Of<ISettingsService>();

        Assert.Throws<ArgumentException>(() => new SportRulesRegistry(new ISportRules[] { new TennisRules(settings), new TennisRules(settings) }));
    }
}
