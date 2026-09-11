using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Tests.Services;

// Smoke test proving the test harness (xUnit + project reference into
// IdTheAthlete.Api) is wired up correctly -- not a full DifficultyService
// spec. Real coverage of the difficulty formulas is deferred follow-up work.
public class DifficultyServiceTests
{
    [Fact]
    public void ComputeDifficultyTier_TennisPlayerWithTwentyTitles_IsEasy()
    {
        var player = new Player
        {
            Name = "Smoke Test Player",
            AttributeValues = new List<PlayerAttributeValue>
            {
                new()
                {
                    Value = "20",
                    AttributeDefinition = new AttributeDefinition { Key = "career_titles" },
                },
            },
        };
        var service = new DifficultyService();

        var tier = service.ComputeDifficultyTier(player, "tennis-men");

        Assert.Equal("easy", tier);
    }
}
