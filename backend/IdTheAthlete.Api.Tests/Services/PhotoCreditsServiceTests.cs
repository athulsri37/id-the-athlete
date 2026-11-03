using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace IdTheAthlete.Api.Tests.Services;

public class PhotoCreditsServiceTests
{
    [Fact]
    public async Task ListsEveryPhoto_ByTourThenPlayer_AndLeavesOutPlayersWithoutOne()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Sports.AddRange(
            new Sport { Id = 1, Slug = "tennis-women", Name = "Women's Tennis", Family = "tennis" },
            new Sport { Id = 2, Slug = "tennis-men", Name = "Men's Tennis", Family = "tennis" });
        db.Players.AddRange(
            new Player { SportId = 1, Name = "Zoe", PhotoFile = "Zoe.jpg", PhotoCredit = "A", PhotoLicense = "CC0" },
            new Player { SportId = 1, Name = "Anna", PhotoFile = "Anna.jpg", PhotoCredit = "B", PhotoLicense = "CC BY 2.0" },
            new Player { SportId = 1, Name = "No Photo" },
            new Player { SportId = 2, Name = "Max", PhotoFile = "Max.png", PhotoCredit = "C", PhotoLicense = "CC BY-SA 4.0" });
        await db.SaveChangesAsync();

        var credits = await new PhotoCreditsService(db).GetPhotoCreditsAsync(CancellationToken.None);

        Assert.Equal(
            new[] { ("tennis-men", "Max", "Max.png", "C", "CC BY-SA 4.0"), ("tennis-women", "Anna", "Anna.jpg", "B", "CC BY 2.0"), ("tennis-women", "Zoe", "Zoe.jpg", "A", "CC0") },
            credits.Select(c => (c.SportSlug, c.PlayerName, c.File, c.Credit, c.License)));
    }
}
