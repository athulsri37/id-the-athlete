using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Rules;

namespace IdTheAthlete.Api.Services;

// A player's practice-mode difficulty tier: the curator's override when one
// is set (e.g. a well-known player whose title count alone would compute too
// hard), otherwise the tier their sport's rules compute from their stats.
// Registered Scoped (depends on ISportRulesRegistry).
public sealed class DifficultyService : IDifficultyService
{
    private readonly ISportRulesRegistry _sportRules;

    public DifficultyService(ISportRulesRegistry sportRules)
    {
        _sportRules = sportRules;
    }

    public string ComputeDifficultyTier(Player player, Sport sport)
    {
        if (player.IsOverridden && !string.IsNullOrWhiteSpace(player.DifficultyOverride))
            return player.DifficultyOverride.ToLowerInvariant();

        return _sportRules.For(sport).ComputeDifficultyTier(player, sport.Slug);
    }
}
