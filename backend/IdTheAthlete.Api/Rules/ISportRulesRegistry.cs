using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Rules;

public interface ISportRulesRegistry
{
    // The rules for this sport. Throws if its family has no rules, or the
    // rules don't support this particular tour.
    ISportRules For(Sport sport);

    // Throws one error naming every sport that couldn't be scored. Called at
    // startup so a missing rule stops the app instead of a game.
    void EnsureEverySportIsSupported(IEnumerable<Sport> sports);
}
