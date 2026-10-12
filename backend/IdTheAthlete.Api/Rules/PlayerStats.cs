using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;

namespace IdTheAthlete.Api.Rules;

internal static class PlayerStats
{
    // A player's numeric stat, read with the same rule the game and the admin
    // tool use (see NumericAttributeValue). A missing or invalid value counts
    // as 0, which makes that stat contribute nothing towards an easier tier.
    // Not logged here: difficulty runs for every player on every practice
    // start, and GameService already warns about the bad value when guessed.
    public static decimal GetNumber(Player player, string attributeKey)
    {
        var value = player.AttributeValues.FirstOrDefault(v => v.AttributeDefinition?.Key == attributeKey)?.Value;
        return NumericAttributeValue.TryParse(value, out var number) ? number : 0;
    }
}
