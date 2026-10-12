using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Rules;

// Scoring rules shared by every Cricket tour. The closeness rules are the same
// for all of them; only the difficulty thresholds differ per tour. Registered
// Scoped (depends on ISettingsService for the closeness settings).
public sealed class CricketRules : ISportRules
{
    private const string RoleClosenessFlag = "CricketRoleClosenessEnabled";
    private const string BowlingStyleClosenessFlag = "CricketBowlingStyleClosenessEnabled";

    // Difficulty thresholds on combined (all-format) career totals: a player
    // reaching any Easy bar is Easy, else any Medium bar is Medium, else Hard.
    private sealed record DifficultyThresholds(
        decimal EasyRuns, decimal EasyWickets, decimal EasyMatches,
        decimal MediumRuns, decimal MediumWickets, decimal MediumMatches);

    // Calibrated per tour rather than shared. Against the 120-player Women's
    // roster, the Men's thresholds only ever placed 11 players in Easy and
    // pushed 46 into Hard despite a solidly-experienced median career
    // (139 matches / 1912 runs / 75 wickets), because the rosters'
    // career-length distributions differ enough that one shared scale doesn't
    // serve both fairly. A new Cricket tour needs its own entry here; until it
    // has one, Supports returns false and the app refuses to start.
    private static readonly Dictionary<string, DifficultyThresholds> ThresholdsByTour = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cricket-men-international"] = new(10000, 300, 300, 3000, 100, 150),
        ["cricket-women-international"] = new(8000, 250, 250, 2500, 90, 130),
    };

    // Numeric closeness: within a percentage of the mystery player's value,
    // with a floor so a small value (e.g. a bowler on 8 wickets) doesn't get an
    // unreasonably tiny window. Both numbers are AppSettings, so they can be
    // retuned live from the admin tool. debut_year deliberately has none.
    private static readonly Dictionary<string, (string PercentKey, string FloorKey)> NumericClosenessSettingKeys = new()
    {
        ["combined_matches"] = ("CricketMatchesClosenessPercent", "CricketMatchesClosenessFloor"),
        ["combined_runs"] = ("CricketRunsClosenessPercent", "CricketRunsClosenessFloor"),
        ["combined_wickets"] = ("CricketWicketsClosenessPercent", "CricketWicketsClosenessFloor"),
    };

    // Country closeness by cricket region, NOT land borders (which are wrong
    // for cricket: Pakistan and Bangladesh are closely linked cricket nations
    // with no shared border, and Australia-New Zealand, cricket's defining
    // rivalry, shares none either). Always on, deliberately with no flag: it's
    // core comparison logic, not an experimental feature. A country not listed
    // has no region and is never close (an exact match still works).
    private static readonly Dictionary<string, string> CountryRegion = new(StringComparer.OrdinalIgnoreCase)
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

    // Two different roles are close if they share at least one tag.
    private static readonly Dictionary<string, string[]> RoleTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Batter"] = new[] { "Batting" },
        ["Wicketkeeper-Batter"] = new[] { "Batting" },
        ["Batting All-rounder"] = new[] { "Batting", "All-Rounder" },
        ["Bowler"] = new[] { "Bowling" },
        ["Bowling All-rounder"] = new[] { "Bowling", "All-Rounder" },
        ["All-rounder"] = new[] { "All-Rounder" },
    };

    // Bowling styles are close when both are pace or both are spin, whichever
    // arm. "Hasn't Bowled" has no group, so it's never close to anything.
    private static readonly Dictionary<string, string> BowlingStyleGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Right-arm Pace"] = "Pace",
        ["Left-arm Pace"] = "Pace",
        ["Right-arm Spin"] = "Spin",
        ["Left-arm Spin"] = "Spin",
    };

    private readonly ISettingsService _settings;

    public CricketRules(ISettingsService settings)
    {
        _settings = settings;
    }

    public string Family => SportFamilies.Cricket;

    public bool Supports(string sportSlug) => ThresholdsByTour.ContainsKey(sportSlug);

    public string ComputeDifficultyTier(Player player, string sportSlug)
    {
        if (!ThresholdsByTour.TryGetValue(sportSlug, out var t))
            throw new InvalidOperationException($"No Cricket difficulty thresholds for '{sportSlug}'. Add them to CricketRules.");

        var runs = PlayerStats.GetNumber(player, "combined_runs");
        var wickets = PlayerStats.GetNumber(player, "combined_wickets");
        var matches = PlayerStats.GetNumber(player, "combined_matches");

        if (runs >= t.EasyRuns || wickets >= t.EasyWickets || matches >= t.EasyMatches)
            return "easy";

        if (runs >= t.MediumRuns || wickets >= t.MediumWickets || matches >= t.MediumMatches)
            return "medium";

        return "hard";
    }

    // Two flag reads and one batched read of every percent/floor value, so a
    // guess costs the same number of round-trips however many clues it has.
    // A missing or unparseable percent/floor means no closeness for that clue.
    public async Task<IClosenessJudge> LoadClosenessJudgeAsync()
    {
        var numericKeys = NumericClosenessSettingKeys.Values.SelectMany(k => new[] { k.PercentKey, k.FloorKey });

        return new Judge(
            await _settings.GetDecimalsAsync(numericKeys),
            await _settings.IsEnabledAsync(RoleClosenessFlag),
            await _settings.IsEnabledAsync(BowlingStyleClosenessFlag));
    }

    private sealed class Judge(
        Dictionary<string, decimal> numericSettings,
        bool roleClosenessEnabled,
        bool bowlingStyleClosenessEnabled) : IClosenessJudge
    {
        public bool IsNumericClose(string attributeKey, decimal guessed, decimal mystery)
        {
            if (!NumericClosenessSettingKeys.TryGetValue(attributeKey, out var keys) ||
                !numericSettings.TryGetValue(keys.PercentKey, out var percent) ||
                !numericSettings.TryGetValue(keys.FloorKey, out var floor))
                return false;

            var threshold = Math.Max(mystery * (percent / 100m), floor);
            return Math.Abs(mystery - guessed) <= threshold;
        }

        public bool IsCategoricalClose(string attributeKey, string guessed, string mystery) => attributeKey switch
        {
            "country" => SameGroup(CountryRegion, guessed, mystery),
            "role" => roleClosenessEnabled && ShareRoleTag(guessed, mystery),
            "bowling_style" => bowlingStyleClosenessEnabled && SameGroup(BowlingStyleGroup, guessed, mystery),
            _ => false,
        };

        private static bool SameGroup(Dictionary<string, string> groups, string a, string b) =>
            groups.TryGetValue(a, out var groupA) &&
            groups.TryGetValue(b, out var groupB) &&
            string.Equals(groupA, groupB, StringComparison.OrdinalIgnoreCase);

        private static bool ShareRoleTag(string a, string b) =>
            RoleTags.TryGetValue(a, out var tagsA) &&
            RoleTags.TryGetValue(b, out var tagsB) &&
            tagsA.Intersect(tagsB, StringComparer.OrdinalIgnoreCase).Any();
    }
}
