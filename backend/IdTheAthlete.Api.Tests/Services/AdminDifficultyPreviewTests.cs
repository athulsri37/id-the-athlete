using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

// The roster agent asks for tiers of players that aren't in the database yet;
// they must come out exactly as the game would compute them once added.
public class AdminDifficultyPreviewTests
{
    private static AdminService CreateService(out GameDbContext db)
    {
        db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Sports.Add(new Sport { Id = 1, Slug = "cricket-women-international", Name = "Women's International Cricket", Family = "cricket" });
        foreach (var (key, id) in new[] { ("combined_runs", 1), ("combined_wickets", 2), ("combined_matches", 3), ("role", 4) })
            db.AttributeDefinitions.Add(new AttributeDefinition { Id = id, SportId = 1, Key = key, Label = key });
        db.SaveChanges();

        var registry = TestGameServiceFactory.CreateRegistry(Mock.Of<ISettingsService>());
        return new AdminService(db, TimeProvider.System, Mock.Of<ISettingsService>(), new DifficultyService(registry));
    }

    [Fact]
    public async Task Preview_UsesThatTourOwnThresholds_InRequestOrder()
    {
        var service = CreateService(out _);

        var tiers = await service.PreviewDifficultyTiersAsync("cricket-women-international", new()
        {
            new() { ["combined_runs"] = "8000", ["combined_wickets"] = "0", ["combined_matches"] = "100" }, // Women's easy bar
            new() { ["combined_runs"] = "100", ["combined_wickets"] = "90", ["combined_matches"] = "50" },  // Women's medium bar
            new() { ["combined_runs"] = "100", ["combined_wickets"] = "10", ["combined_matches"] = "20", ["role"] = "Bowler" },
        });

        Assert.Equal(new[] { "easy", "medium", "hard" }, tiers);
    }

    [Fact]
    public async Task Preview_IgnoresUnknownKeysAndTreatsBadNumbersAsZero()
    {
        var service = CreateService(out _);

        var tiers = await service.PreviewDifficultyTiersAsync("cricket-women-international", new()
        {
            new() { ["not_an_attribute"] = "999999", ["combined_runs"] = "lots" },
        });

        Assert.Equal(new[] { "hard" }, tiers);
    }

    [Fact]
    public async Task Preview_UnknownSport_Throws()
    {
        var service = CreateService(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => service.PreviewDifficultyTiersAsync("no-such-sport", new()));
    }

    [Fact]
    public async Task Preview_DoesNotSaveAnything()
    {
        var service = CreateService(out var db);

        await service.PreviewDifficultyTiersAsync("cricket-women-international", new() { new() { ["combined_runs"] = "5" } });

        Assert.Empty(db.Players);
        Assert.Empty(db.PlayerAttributeValues);
    }
}
