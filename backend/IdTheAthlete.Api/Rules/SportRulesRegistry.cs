using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Rules;

// Maps each sport to the ISportRules registered for its family. Replaces the
// old per-slug checks that silently fell back to Tennis rules for any sport
// they didn't recognise: an unknown family or unsupported tour is now an error.
// Registered Scoped, like the rules it holds.
public sealed class SportRulesRegistry : ISportRulesRegistry
{
    private readonly Dictionary<string, ISportRules> _rulesByFamily;

    public SportRulesRegistry(IEnumerable<ISportRules> rules)
    {
        // ToDictionary throws on a duplicate family, which is a registration
        // mistake worth failing on.
        _rulesByFamily = rules.ToDictionary(r => r.Family, StringComparer.OrdinalIgnoreCase);
    }

    public ISportRules For(Sport sport)
    {
        var problem = FindProblem(sport, out var rules);
        return problem == null ? rules! : throw new InvalidOperationException(problem);
    }

    public void EnsureEverySportIsSupported(IEnumerable<Sport> sports)
    {
        var problems = sports
            .Select(sport => FindProblem(sport, out _))
            .OfType<string>()
            .ToList();

        if (problems.Count > 0)
            throw new InvalidOperationException(
                "Some sports have no scoring rules: " + string.Join(" ", problems));
    }

    private string? FindProblem(Sport sport, out ISportRules? rules)
    {
        if (!_rulesByFamily.TryGetValue(sport.Family, out rules))
            return $"Sport '{sport.Slug}' has family '{sport.Family}', which has no ISportRules implementation " +
                   $"(known families: {string.Join(", ", _rulesByFamily.Keys.Order())}). " +
                   "Fix Sports.Family or add and register rules for the new family.";

        if (!rules.Supports(sport.Slug))
            return $"Sport '{sport.Slug}' is in family '{sport.Family}', but {rules.GetType().Name} doesn't support it yet " +
                   "(e.g. it has no difficulty thresholds for this tour).";

        return null;
    }
}
