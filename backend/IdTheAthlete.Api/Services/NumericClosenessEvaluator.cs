namespace IdTheAthlete.Api.Services;

// Numeric "close" (amber) evaluation for both Tennis and Cricket, kept as
// two clearly separate rules sharing nothing but this class:
// - Tennis: fixed absolute thresholds, hardcoded below.
// - Cricket: percent-of-actual-value with a floor, read fresh from
//   AppSettings on every guess (LoadCricketSettingsAsync), not hardcoded,
//   so it can be retuned live without a redeploy.
// Registered Scoped (depends on ISettingsService for the Cricket settings read).
public class NumericClosenessEvaluator : INumericClosenessEvaluator
{
    private readonly ISettingsService _settings;

    // Tennis-only: fixed absolute closeness thresholds, untouched by the
    // Cricket closeness logic below (entirely separate code path).
    private static readonly Dictionary<string, decimal> NumericCloseThresholds = new()
    {
        ["grand_slam_titles"] = 2,
        ["career_high_ranking"] = 5,
        ["turned_pro_year"] = 3,
        ["career_titles"] = 5,
    };

    // Cricket-only: percent-of-actual-value closeness, with a floor so a
    // player with a small actual value (e.g. a bowler on 8 wickets) doesn't
    // get an unreasonably tiny closeness window. debut_year deliberately
    // has no closeness tier.
    private static readonly Dictionary<string, (string PercentKey, string FloorKey)> CricketNumericClosenessSettingKeys = new()
    {
        ["combined_matches"] = ("CricketMatchesClosenessPercent", "CricketMatchesClosenessFloor"),
        ["combined_runs"] = ("CricketRunsClosenessPercent", "CricketRunsClosenessFloor"),
        ["combined_wickets"] = ("CricketWicketsClosenessPercent", "CricketWicketsClosenessFloor"),
    };

    public NumericClosenessEvaluator(ISettingsService settings)
    {
        _settings = settings;
    }

    // Fetches every Cricket percent/floor AppSettings value in a single
    // round-trip. Call once per guess and pass the result into IsClose for
    // each numeric attribute -- not once per attribute, to avoid turning
    // one guess into several extra DB round-trips. A missing or unparseable
    // value is simply absent, meaning "no closeness for this attribute".
    public async Task<Dictionary<string, decimal>> LoadCricketSettingsAsync()
    {
        var keys = CricketNumericClosenessSettingKeys.Values.SelectMany(k => new[] { k.PercentKey, k.FloorKey });
        return await _settings.GetDecimalsAsync(keys);
    }

    // Only ever called when guessedNum != mysteryNum (an exact match is
    // handled separately by the caller and never reaches here).
    public bool IsClose(string attributeKey, decimal guessedNum, decimal mysteryNum, Dictionary<string, decimal> cricketSettings)
    {
        if (CricketNumericClosenessSettingKeys.TryGetValue(attributeKey, out var settingKeys) &&
            cricketSettings.TryGetValue(settingKeys.PercentKey, out var percent) &&
            cricketSettings.TryGetValue(settingKeys.FloorKey, out var floor))
        {
            var threshold = Math.Max(mysteryNum * (percent / 100m), floor);
            return Math.Abs(mysteryNum - guessedNum) <= threshold;
        }

        if (NumericCloseThresholds.TryGetValue(attributeKey, out var tennisThreshold))
        {
            return Math.Abs(mysteryNum - guessedNum) <= tennisThreshold;
        }

        return false;
    }
}
