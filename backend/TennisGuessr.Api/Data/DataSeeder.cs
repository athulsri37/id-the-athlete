using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Models;

namespace TennisGuessr.Api.Data;

// Seeds a 20-player ATP sample dataset spanning three eras, matching the
// era-weighted design used for difficulty tiers (current / 2005-2015 / legend).
// Stats are accurate as of mid-2026; re-run/update seeding periodically as
// rankings and title counts change for active players.
public static class DataSeeder
{
    private static readonly HashSet<string> RetiredPlayers = new()
    {
        "Pete Sampras", "Andre Agassi", "Boris Becker", "John McEnroe",
        "Roger Federer", "Rafael Nadal", "Lleyton Hewitt", "David Ferrer",
    };

    public static async Task SeedAsync(GameDbContext db)
    {
        await SeedTennisAsync(db);
        await SeedActiveStatusAttributeAsync(db);
        await SeedAppSettingsAsync(db);
    }

    private static async Task SeedTennisAsync(GameDbContext db)
    {
        if (await db.Sports.AnyAsync(s => s.Slug == "tennis"))
            return; // already seeded

        var tennis = new Sport { Name = "Tennis", Slug = "tennis" };
        db.Sports.Add(tennis);
        await db.SaveChangesAsync();

        var attributeDefs = new List<AttributeDefinition>
        {
            new() { SportId = tennis.Id, Key = "plays",               Label = "Plays",              Type = AttributeType.Categorical, DisplayOrder = 1 },
            new() { SportId = tennis.Id, Key = "backhand",             Label = "Backhand",           Type = AttributeType.Categorical, DisplayOrder = 2 },
            new() { SportId = tennis.Id, Key = "country",              Label = "Country",            Type = AttributeType.Categorical, DisplayOrder = 3 },
            new() { SportId = tennis.Id, Key = "grand_slam_titles",    Label = "Grand Slams",        Type = AttributeType.Numeric,     DisplayOrder = 4 },
            new() { SportId = tennis.Id, Key = "career_high_ranking",  Label = "Career-High Rank",   Type = AttributeType.Numeric,     DisplayOrder = 5 },
            new() { SportId = tennis.Id, Key = "turned_pro_year",      Label = "Turned Pro",         Type = AttributeType.Numeric,     DisplayOrder = 6 },
            new() { SportId = tennis.Id, Key = "career_titles",        Label = "Career Titles",      Type = AttributeType.Numeric,     DisplayOrder = 7 },
        };
        db.AttributeDefinitions.AddRange(attributeDefs);
        await db.SaveChangesAsync();

        var attrByKey = attributeDefs.ToDictionary(a => a.Key, a => a.Id);

        var players = new (string Name, string Era, string Plays, string Backhand, string Country, int Slams, int HighRank, int ProYear, int Titles)[]
        {
            ("Pete Sampras", "legend", "Right", "One-Handed", "USA", 14, 1, 1988, 64),
            ("Andre Agassi", "legend", "Right", "Two-Handed", "USA", 8, 1, 1986, 60),
            ("Boris Becker", "legend", "Right", "One-Handed", "Germany", 6, 1, 1984, 49),
            ("John McEnroe", "legend", "Left", "One-Handed", "USA", 7, 1, 1978, 77),

            ("Roger Federer", "2005_2015", "Right", "One-Handed", "Switzerland", 20, 1, 1998, 103),
            ("Rafael Nadal", "2005_2015", "Left", "Two-Handed", "Spain", 22, 1, 2001, 92),
            ("Novak Djokovic", "2005_2015", "Right", "Two-Handed", "Serbia", 24, 1, 2003, 100),
            ("Andy Murray", "2005_2015", "Right", "Two-Handed", "United Kingdom", 3, 1, 2005, 46),
            ("Stan Wawrinka", "2005_2015", "Right", "One-Handed", "Switzerland", 3, 3, 2002, 16),
            ("Lleyton Hewitt", "2005_2015", "Right", "Two-Handed", "Australia", 2, 1, 1998, 30),
            ("David Ferrer", "2005_2015", "Right", "Two-Handed", "Spain", 0, 3, 2000, 27),

            ("Carlos Alcaraz", "current", "Right", "Two-Handed", "Spain", 7, 1, 2018, 22),
            ("Jannik Sinner", "current", "Right", "Two-Handed", "Italy", 5, 1, 2018, 20),
            ("Daniil Medvedev", "current", "Right", "Two-Handed", "Russia", 1, 1, 2014, 20),
            ("Alexander Zverev", "current", "Right", "Two-Handed", "Germany", 0, 2, 2013, 24),
            ("Stefanos Tsitsipas", "current", "Right", "One-Handed", "Greece", 0, 3, 2016, 12),
            ("Casper Ruud", "current", "Right", "Two-Handed", "Norway", 0, 2, 2015, 10),
            ("Taylor Fritz", "current", "Right", "Two-Handed", "USA", 0, 4, 2015, 9),
            ("Holger Rune", "current", "Right", "Two-Handed", "Denmark", 0, 4, 2019, 6),
            ("Andrey Rublev", "current", "Right", "Two-Handed", "Russia", 0, 5, 2014, 17),
        };

        foreach (var p in players)
        {
            var player = new Player { SportId = tennis.Id, Name = p.Name, EraGroup = p.Era };
            db.Players.Add(player);
            await db.SaveChangesAsync();

            db.PlayerAttributeValues.AddRange(new[]
            {
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["plays"], Value = p.Plays },
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["backhand"], Value = p.Backhand },
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["country"], Value = p.Country },
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["grand_slam_titles"], Value = p.Slams.ToString() },
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["career_high_ranking"], Value = p.HighRank.ToString() },
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["turned_pro_year"], Value = p.ProYear.ToString() },
                new PlayerAttributeValue { PlayerId = player.Id, AttributeDefinitionId = attrByKey["career_titles"], Value = p.Titles.ToString() },
            });
        }

        await db.SaveChangesAsync();
    }

    // Runs unconditionally on every startup (unlike SeedTennisAsync, which
    // bails out entirely once the sport exists) so new attributes can be
    // added to a database that was already seeded in the past. Both the
    // AttributeDefinition and each player's value are checked and inserted
    // independently, so re-running is always safe.
    private static async Task SeedActiveStatusAttributeAsync(GameDbContext db)
    {
        var tennis = await db.Sports.FirstOrDefaultAsync(s => s.Slug == "tennis");
        if (tennis == null)
            return; // tennis hasn't been seeded yet; nothing to attach this to

        var activeStatusDef = await db.AttributeDefinitions
            .FirstOrDefaultAsync(a => a.SportId == tennis.Id && a.Key == "active_status");

        if (activeStatusDef == null)
        {
            activeStatusDef = new AttributeDefinition
            {
                SportId = tennis.Id,
                Key = "active_status",
                Label = "Status",
                Type = AttributeType.Categorical,
                DisplayOrder = 0, // sorts before Plays (1) so it leads the clue row
            };
            db.AttributeDefinitions.Add(activeStatusDef);
            await db.SaveChangesAsync();
        }

        var players = await db.Players.Where(p => p.SportId == tennis.Id).ToListAsync();

        var playerIdsWithValue = await db.PlayerAttributeValues
            .Where(v => v.AttributeDefinitionId == activeStatusDef.Id)
            .Select(v => v.PlayerId)
            .ToListAsync();
        var playerIdsWithValueSet = playerIdsWithValue.ToHashSet();

        var missingValues = players
            .Where(p => !playerIdsWithValueSet.Contains(p.Id))
            .Select(p => new PlayerAttributeValue
            {
                PlayerId = p.Id,
                AttributeDefinitionId = activeStatusDef.Id,
                Value = RetiredPlayers.Contains(p.Name) ? "Retired" : "Active",
            });

        db.PlayerAttributeValues.AddRange(missingValues);
        await db.SaveChangesAsync();
    }

    private static async Task SeedAppSettingsAsync(GameDbContext db)
    {
        if (!await db.AppSettings.AnyAsync(s => s.Key == "ActiveTheme"))
        {
            db.AppSettings.Add(new AppSetting { Key = "ActiveTheme", Value = "retro" });
        }

        if (!await db.AppSettings.AnyAsync(s => s.Key == "AiTriviaEnabled"))
        {
            db.AppSettings.Add(new AppSetting { Key = "AiTriviaEnabled", Value = "false" });
        }

        await db.SaveChangesAsync();
    }
}