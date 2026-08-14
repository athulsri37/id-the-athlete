using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Geo;

namespace IdTheAthlete.Api.Services;

// Categorical "close" (amber) evaluation: Country (both sports, via two
// entirely different rules -- see IsClose below) plus Cricket's Role and
// Bowling Style. Registered Scoped (depends on GameDbContext for the
// AppSettings flag reads).
public class CategoricalClosenessEvaluator
{
    private readonly GameDbContext _db;

    private static readonly HashSet<string> CricketSportSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "cricket-men-international",
        "cricket-women-international",
    };

    // Cricket-only: country closeness via cricket-specific regional blocs,
    // NOT the land-border geography in CountryProximity.cs (which is
    // wrong for cricket -- e.g. Pakistan and Bangladesh are closely linked
    // cricket nations despite no shared land border, and Australia-New
    // Zealand, cricket's defining rivalry, also shares no border). Always
    // active for Cricket -- deliberately no AppSettings flag, unlike
    // CountryClosenessEnabled/CricketRoleClosenessEnabled/etc., since this
    // is a straightforward correctness fix to core comparison logic for
    // Cricket, not an experimental or tunable feature. A country not
    // listed here has no bloc and never counts as close via this
    // mechanism (an exact match still works normally regardless). Tennis
    // keeps using CountryProximity.IsClose below, completely unchanged --
    // this dictionary and CountryClosenessEnabled are never consulted for
    // Cricket's country clue.
    private static readonly Dictionary<string, string> CricketCountryBloc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["India"] = "Asia",
        ["Pakistan"] = "Asia",
        ["Bangladesh"] = "Asia",
        ["Sri Lanka"] = "Asia",
        ["Afghanistan"] = "Asia",
        ["Nepal"] = "Asia",
        ["Australia"] = "Oceania",
        ["New Zealand"] = "Oceania",
        ["England"] = "British Isles",
        ["Ireland"] = "British Isles",
        ["Scotland"] = "British Isles",
        ["Netherlands"] = "British Isles",
        ["South Africa"] = "Africa",
        ["Namibia"] = "Africa",
        ["Zimbabwe"] = "Africa",
        ["West Indies"] = "Americas",
        ["USA"] = "Americas",
        ["Canada"] = "Americas",
    };

    // Cricket-only: Role closeness via tag-based grouping -- two roles
    // (that aren't an exact match) are close if they share at least one
    // tag. Gated by CricketRoleClosenessEnabled, checked fresh per guess
    // the same way as the country/numeric flags. Entirely separate from,
    // and never touches, Tennis's categorical comparison (Plays, Backhand,
    // Active Status), which has no closeness tier at all.
    private static readonly Dictionary<string, string[]> RoleTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Batter"] = new[] { "Batting" },
        ["Wicketkeeper-Batter"] = new[] { "Batting" },
        ["Batting All-rounder"] = new[] { "Batting", "All-Rounder" },
        ["Bowler"] = new[] { "Bowling" },
        ["Bowling All-rounder"] = new[] { "Bowling", "All-Rounder" },
        ["All-rounder"] = new[] { "All-Rounder" },
    };

    // Cricket-only: Bowling Style closeness, grouped by pace vs. spin and
    // ignoring which arm. "Hasn't Bowled" has no entry -- it's isolated,
    // never close to any other value (an exact "Hasn't Bowled" vs. "Hasn't
    // Bowled" pairing is already a match, so it never reaches this check).
    // Gated by CricketBowlingStyleClosenessEnabled.
    private static readonly Dictionary<string, string> BowlingStyleGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Right-arm Pace"] = "Pace",
        ["Left-arm Pace"] = "Pace",
        ["Right-arm Spin"] = "Spin",
        ["Left-arm Spin"] = "Spin",
    };

    public CategoricalClosenessEvaluator(GameDbContext db)
    {
        _db = db;
    }

    // Fetches every categorical-closeness AppSettings flag once, so the
    // caller can pass the same snapshot into IsClose for each attribute
    // in a guess without a DB round-trip per attribute.
    public async Task<CategoricalClosenessFlags> LoadFlagsAsync()
    {
        return new CategoricalClosenessFlags
        {
            CountryClosenessEnabled = await IsAppSettingEnabledAsync("CountryClosenessEnabled"),
            CricketRoleClosenessEnabled = await IsAppSettingEnabledAsync("CricketRoleClosenessEnabled"),
            CricketBowlingStyleClosenessEnabled = await IsAppSettingEnabledAsync("CricketBowlingStyleClosenessEnabled"),
        };
    }

    // General-purpose boolean AppSettings flag, read fresh (no caching)
    // every time it's called.
    private async Task<bool> IsAppSettingEnabledAsync(string key)
    {
        try
        {
            var value = await _db.AppSettings
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    // Only ever called when guessedValue != mysteryValue (an exact match
    // is handled separately by the caller and never reaches here).
    public bool IsClose(string attributeKey, string sportSlug, string guessedValue, string mysteryValue, CategoricalClosenessFlags flags)
    {
        if (attributeKey == "country" && CricketSportSlugs.Contains(sportSlug))
        {
            // Cricket: regional bloc, unconditional -- no flag.
            return AreCricketCountriesClose(guessedValue, mysteryValue);
        }

        if (attributeKey == "country" && flags.CountryClosenessEnabled)
        {
            // Tennis (and any other non-Cricket sport): unchanged.
            return CountryProximity.IsClose(guessedValue, mysteryValue);
        }

        if (attributeKey == "role" && flags.CricketRoleClosenessEnabled)
        {
            return AreCricketRolesClose(guessedValue, mysteryValue);
        }

        if (attributeKey == "bowling_style" && flags.CricketBowlingStyleClosenessEnabled)
        {
            return AreCricketBowlingStylesClose(guessedValue, mysteryValue);
        }

        return false;
    }

    private static bool AreCricketCountriesClose(string guessedCountry, string mysteryCountry)
    {
        return CricketCountryBloc.TryGetValue(guessedCountry, out var guessedBloc) &&
               CricketCountryBloc.TryGetValue(mysteryCountry, out var mysteryBloc) &&
               string.Equals(guessedBloc, mysteryBloc, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AreCricketRolesClose(string guessedRole, string mysteryRole)
    {
        return RoleTags.TryGetValue(guessedRole, out var guessedTags) &&
               RoleTags.TryGetValue(mysteryRole, out var mysteryTags) &&
               guessedTags.Intersect(mysteryTags, StringComparer.OrdinalIgnoreCase).Any();
    }

    private static bool AreCricketBowlingStylesClose(string guessedStyle, string mysteryStyle)
    {
        return BowlingStyleGroup.TryGetValue(guessedStyle, out var guessedGroup) &&
               BowlingStyleGroup.TryGetValue(mysteryStyle, out var mysteryGroup) &&
               string.Equals(guessedGroup, mysteryGroup, StringComparison.OrdinalIgnoreCase);
    }
}

public class CategoricalClosenessFlags
{
    public bool CountryClosenessEnabled { get; init; }
    public bool CricketRoleClosenessEnabled { get; init; }
    public bool CricketBowlingStyleClosenessEnabled { get; init; }
}
