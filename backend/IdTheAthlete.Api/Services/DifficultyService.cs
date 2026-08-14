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

        // Applied uniformly to both cricket-men-international and
        // cricket-women-international for now -- a Women's-specific
        // formula is a planned future refinement, not implemented yet.
        if (sportSlug is "cricket-men-international" or "cricket-women-international")
            return ComputeCricketDifficultyTier(player);

        return ComputeTennisDifficultyTier(player);
    }

    // Checked in order -- easy, then medium, then hard as the fallback --
    // so the hard branch never needs its own explicit condition: by the
    // time a player falls through both easy (high rank #1 or 20+ titles)
    // and medium (5-19 titles), they're guaranteed to have never reached
    // #1 and have fewer than 5 titles, which already satisfies the stated
    // hard rule.
    private static string ComputeTennisDifficultyTier(Player player)
    {
        var highRank = GetNumericAttribute(player, "career_high_ranking");
        var titles = GetNumericAttribute(player, "career_titles");

        if (highRank == 1 || titles >= 20)
            return "easy";

        if (titles >= 5 && titles < 20)
            return "medium";

        return "hard";
    }

    // Based on combined (all-format) career totals, checked in the same
    // easy/medium/hard-fallback order as Tennis above.
    private static string ComputeCricketDifficultyTier(Player player)
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

    private static int GetNumericAttribute(Player player, string key)
    {
        var value = player.AttributeValues.FirstOrDefault(v => v.AttributeDefinition?.Key == key)?.Value;
        return value != null && int.TryParse(value, out var parsed) ? parsed : 0;
    }
}
