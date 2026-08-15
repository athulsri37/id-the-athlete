using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Computes a player's practice-mode difficulty tier from their stats,
// unless a curator has explicitly overridden it (e.g. a well-known player
// whose title count alone would compute too hard). The formula is
// sport-specific since each sport's AttributeDefinitions differ; sportSlug
// picks which one applies. Pure/stateless -- registered as a Singleton.
public class DifficultyService
{
    public string ComputeDifficultyTier(Player player, string sportSlug)
    {
        if (player.IsOverridden && !string.IsNullOrWhiteSpace(player.DifficultyOverride))
            return player.DifficultyOverride!.ToLowerInvariant();

        if (sportSlug == "cricket-women-international")
            return ComputeCricketWomenDifficultyTier(player);

        if (sportSlug == "cricket-men-international")
            return ComputeCricketMenDifficultyTier(player);

        return ComputeTennisDifficultyTier(player);
    }

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
    // per-sport calibration). A single Slam is deliberately NOT enough on
    // its own (e.g. Raducanu, Andreescu, Vondroušová, Thiem stay put) --
    // one major alone is too easy to win from a thin career to be treated
    // as equivalent to being #1 or a 20-title veteran.
    private static string ComputeTennisDifficultyTier(Player player)
    {
        var highRank = GetNumericAttribute(player, "career_high_ranking");
        var titles = GetNumericAttribute(player, "career_titles");
        var grandSlams = GetNumericAttribute(player, "grand_slam_titles");

        if (highRank == 1 || titles >= 20 || grandSlams >= 2)
            return "easy";

        if (titles >= 5 && titles < 20)
            return "medium";

        return "hard";
    }

    // Based on combined (all-format) career totals, checked in the same
    // easy/medium/hard-fallback order as Tennis above.
    private static string ComputeCricketMenDifficultyTier(Player player)
    {
        var runs = GetNumericAttribute(player, "combined_runs");
        var wickets = GetNumericAttribute(player, "combined_wickets");
        var matches = GetNumericAttribute(player, "combined_matches");

        if (runs >= 10000 || wickets >= 300 || matches >= 300)
            return "easy";

        if (runs >= 3000 || wickets >= 100 || matches >= 150)
            return "medium";

        return "hard";
    }

    // Women's Cricket-specific thresholds, calibrated separately from the
    // Men's formula above rather than reused -- against the current
    // 120-player Women's roster, the Men's thresholds only ever placed 11
    // players in Easy and pushed 46 into Hard despite a solidly-experienced
    // median career (139 matches / 1912 runs / 75 wickets), because the
    // rosters' career-length distributions differ enough that one shared
    // scale doesn't serve both fairly. Same easy/medium/hard-fallback
    // structure and attribute keys as the Men's formula, just lower bars.
    private static string ComputeCricketWomenDifficultyTier(Player player)
    {
        var runs = GetNumericAttribute(player, "combined_runs");
        var wickets = GetNumericAttribute(player, "combined_wickets");
        var matches = GetNumericAttribute(player, "combined_matches");

        if (runs >= 8000 || wickets >= 250 || matches >= 250)
            return "easy";

        if (runs >= 2500 || wickets >= 90 || matches >= 130)
            return "medium";

        return "hard";
    }

    private static int GetNumericAttribute(Player player, string key)
    {
        var value = player.AttributeValues.FirstOrDefault(v => v.AttributeDefinition?.Key == key)?.Value;
        return value != null && int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
