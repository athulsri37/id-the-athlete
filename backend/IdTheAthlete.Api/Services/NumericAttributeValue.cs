using System.Globalization;

namespace IdTheAthlete.Api.Services;

// The single definition of a valid numeric attribute value (PlayerAttributeValue
// stores every value as text). Shared by gameplay (GameService) and the admin
// tool's validation (AdminService), so the admin tool can never save a value
// the game can't then read. Parsed with the invariant culture so "2.5" means
// 2.5 whatever locale the server runs in. Unlike NumberStyles.Number, commas
// are rejected rather than read as thousands separators, so "2,5" typed in the
// admin tool is an error instead of silently becoming 25.
public static class NumericAttributeValue
{
    private const NumberStyles Styles =
        NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;

    public static bool TryParse(string? value, out decimal number) =>
        decimal.TryParse(value, Styles, CultureInfo.InvariantCulture, out number);
}
