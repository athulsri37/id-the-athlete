using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Rules;

// Everything about scoring a game that differs between sports, for one sport
// family (Sport.Family). SportRulesRegistry picks the implementation for a
// sport. A new tour of an existing family is data plus anything its rules
// need per tour (see Supports); a new sport is data plus a new implementation
// of this interface, registered in Program.cs.
public interface ISportRules
{
    // The Sport.Family value this implementation handles, e.g. "tennis".
    string Family { get; }

    // Whether this family has everything it needs to score this particular
    // tour (e.g. difficulty thresholds). Checked for every sport at startup.
    bool Supports(string sportSlug);

    // "easy", "medium" or "hard" from the player's stats alone. Curator
    // overrides are applied by DifficultyService before this is called.
    string ComputeDifficultyTier(Player player, string sportSlug);

    // Reads the settings this family's closeness rules depend on (fresh, never
    // cached, so admin changes apply on the next guess) and returns a judge
    // that applies them to every clue of one guess.
    Task<IClosenessJudge> LoadClosenessJudgeAsync();
}
