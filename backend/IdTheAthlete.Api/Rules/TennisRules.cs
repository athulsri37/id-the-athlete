using IdTheAthlete.Api.Geo;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Rules;

// Scoring rules shared by every Tennis tour (Men's and Women's). Registered
// Scoped (depends on ISettingsService for the country-closeness flag).
public sealed class TennisRules : ISportRules
{
    private const string CountryClosenessFlag = "CountryClosenessEnabled";

    // Fixed absolute closeness thresholds for numeric clues.
    private static readonly Dictionary<string, decimal> NumericCloseThresholds = new()
    {
        ["grand_slam_titles"] = 2,
        ["career_high_ranking"] = 5,
        ["turned_pro_year"] = 3,
        ["career_titles"] = 5,
    };

    private readonly ISettingsService _settings;

    public TennisRules(ISettingsService settings)
    {
        _settings = settings;
    }

    public string Family => SportFamilies.Tennis;

    // One formula serves every Tennis tour: Men's and Women's rosters showed
    // comparable distributions, unlike Cricket's (see CricketRules).
    public bool Supports(string sportSlug) => true;

    // Checked in order -- easy, then medium, then hard as the fallback --
    // so the hard branch never needs its own explicit condition: by the
    // time a player falls through both easy (high rank #1, 20+ titles, or
    // 2+ Grand Slams) and medium (5-19 titles), they're guaranteed to have
    // never reached #1, have fewer than 5 titles, and have at most 1 Grand
    // Slam, which already satisfies the stated hard rule.
    //
    // grand_slams >= 2 was added as an Easy-tier signal because a
    // title-count/ranking-only formula let a handful of multi-Slam
    // champions (e.g. Wawrinka, Kuznetsova) sit in Medium purely for
    // having a modest regular-tour title count -- shared identically
    // across Men's and Women's since both rosters showed the same pattern
    // and comparable distributions (unlike Cricket, which needed separate
    // per-tour calibration). A single Slam is deliberately NOT enough on
    // its own (e.g. Raducanu, Andreescu, Vondroušová, Thiem stay put) --
    // one major alone is too easy to win from a thin career to be treated
    // as equivalent to being #1 or a 20-title veteran.
    public string ComputeDifficultyTier(Player player, string sportSlug)
    {
        var highRank = PlayerStats.GetNumber(player, "career_high_ranking");
        var titles = PlayerStats.GetNumber(player, "career_titles");
        var grandSlams = PlayerStats.GetNumber(player, "grand_slam_titles");

        if (highRank == 1 || titles >= 20 || grandSlams >= 2)
            return "easy";

        if (titles >= 5 && titles < 20)
            return "medium";

        return "hard";
    }

    public async Task<IClosenessJudge> LoadClosenessJudgeAsync()
    {
        return new Judge(await _settings.IsEnabledAsync(CountryClosenessFlag));
    }

    private sealed class Judge(bool countryClosenessEnabled) : IClosenessJudge
    {
        public bool IsNumericClose(string attributeKey, decimal guessed, decimal mystery) =>
            NumericCloseThresholds.TryGetValue(attributeKey, out var threshold) &&
            Math.Abs(mystery - guessed) <= threshold;

        // Country is close when the two share a real land border (or a curated
        // pair like Australia-New Zealand), when the flag is on. Tennis's other
        // categorical clues (Plays, Backhand, Status) have no closeness tier.
        public bool IsCategoricalClose(string attributeKey, string guessed, string mystery) =>
            attributeKey == "country" && countryClosenessEnabled && CountryProximity.IsClose(guessed, mystery);
    }
}
