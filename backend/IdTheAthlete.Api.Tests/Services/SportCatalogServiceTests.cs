using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace IdTheAthlete.Api.Tests.Services;

public class SportCatalogServiceTests
{
    [Fact]
    public async Task GetSports_ReturnsEveryTourWithFamilyAndShortName_OrderedByName()
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Sports.AddRange(
            new Sport { Slug = "tennis-women", Name = "Women's Tennis", ShortName = "Women's", Family = "tennis" },
            new Sport { Slug = "cricket-men-international", Name = "Men's International Cricket", ShortName = "Men's International", Family = "cricket" },
            new Sport { Slug = "tennis-men", Name = "Men's Tennis", ShortName = "Men's", Family = "tennis" });
        await db.SaveChangesAsync();

        var sports = await new SportCatalogService(db).GetSportsAsync();

        Assert.Equal(new[] { "cricket-men-international", "tennis-men", "tennis-women" }, sports.Select(s => s.Slug));
        var menTennis = sports[1];
        Assert.Equal(("Men's Tennis", "Men's", "tennis"), (menTennis.Name, menTennis.ShortName, menTennis.Family));
        // The frontend takes the game's numbers from here, never its own copy.
        Assert.All(sports, s => Assert.Equal((8, 5), (s.MaxGuesses, s.HintAfterGuess)));
    }
}
