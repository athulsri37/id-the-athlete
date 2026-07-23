namespace TennisGuessr.Api.Geo;

// True land-border adjacency data for the closeness-tier clue feature,
// modeled on public country-border datasets (e.g. GeoDataSource's Country
// Borders CSV, github.com/geodatasource/country-borders). A guess counts as
// "close" if the guessed country shares a real land border with the mystery
// country -- checked symmetrically, so it doesn't matter which side lists
// the relationship. This is static geographic fact, not app-specific
// mutable data like player stats, so it's embedded directly in code rather
// than the database.
//
// Neighbor lists include every real land-border neighbor of each country
// present in the Players seed data, even neighbors that aren't themselves
// in the roster, so the data stays correct if the roster grows -- add new
// entries here if a future player batch introduces a country not yet
// listed. A few notable non-obvious real borders are kept deliberately:
// Spain-Morocco (via Ceuta/Melilla), Russia/Lithuania-Poland (via
// Kaliningrad), and United Kingdom-Ireland (via Northern Ireland) --
// because United Kingdom-Ireland already resolves correctly through this
// real adjacency data, it does NOT need a manual override below.
public static class CountryProximity
{
    private static readonly Dictionary<string, string[]> Neighbors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Argentina"] = new[] { "Bolivia", "Brazil", "Chile", "Paraguay", "Uruguay" },
        ["Australia"] = Array.Empty<string>(), // island continent, no land borders
        ["Austria"] = new[] { "Czech Republic", "Germany", "Hungary", "Italy", "Liechtenstein", "Slovakia", "Slovenia", "Switzerland" },
        ["Belgium"] = new[] { "France", "Germany", "Luxembourg", "Netherlands" },
        ["Bosnia and Herzegovina"] = new[] { "Croatia", "Montenegro", "Serbia" },
        ["Brazil"] = new[] { "Argentina", "Bolivia", "Colombia", "French Guiana", "Guyana", "Paraguay", "Peru", "Suriname", "Uruguay", "Venezuela" },
        ["Bulgaria"] = new[] { "Greece", "North Macedonia", "Romania", "Serbia", "Turkey" },
        ["Canada"] = new[] { "USA" },
        ["Chile"] = new[] { "Argentina", "Bolivia", "Peru" },
        ["China"] = new[] { "Afghanistan", "Bhutan", "India", "Kazakhstan", "Kyrgyzstan", "Laos", "Mongolia", "Myanmar", "Nepal", "North Korea", "Pakistan", "Russia", "Tajikistan", "Vietnam" },
        ["Croatia"] = new[] { "Bosnia and Herzegovina", "Hungary", "Montenegro", "Serbia", "Slovenia" },
        ["Cyprus"] = Array.Empty<string>(), // island, no land borders
        ["Czech Republic"] = new[] { "Austria", "Germany", "Poland", "Slovakia" },
        ["Denmark"] = new[] { "Germany" },
        ["Finland"] = new[] { "Norway", "Russia", "Sweden" },
        ["France"] = new[] { "Andorra", "Belgium", "Germany", "Italy", "Luxembourg", "Monaco", "Spain", "Switzerland" },
        ["Germany"] = new[] { "Austria", "Belgium", "Czech Republic", "Denmark", "France", "Luxembourg", "Netherlands", "Poland", "Switzerland" },
        ["Greece"] = new[] { "Albania", "Bulgaria", "North Macedonia", "Turkey" },
        ["Hungary"] = new[] { "Austria", "Croatia", "Romania", "Serbia", "Slovakia", "Slovenia", "Ukraine" },
        ["Italy"] = new[] { "Austria", "France", "San Marino", "Slovenia", "Switzerland", "Vatican City" },
        ["Japan"] = Array.Empty<string>(), // island nation, no land borders
        ["Kazakhstan"] = new[] { "China", "Kyrgyzstan", "Russia", "Turkmenistan", "Uzbekistan" },
        ["Latvia"] = new[] { "Belarus", "Estonia", "Lithuania", "Russia" },
        ["Monaco"] = new[] { "France" },
        ["Morocco"] = new[] { "Algeria", "Spain", "Western Sahara" }, // Spain via Ceuta/Melilla
        ["Netherlands"] = new[] { "Belgium", "Germany" },
        ["Norway"] = new[] { "Finland", "Russia", "Sweden" },
        ["Peru"] = new[] { "Bolivia", "Brazil", "Chile", "Colombia", "Ecuador" },
        ["Poland"] = new[] { "Belarus", "Czech Republic", "Germany", "Lithuania", "Russia", "Slovakia", "Ukraine" }, // Russia via Kaliningrad
        ["Portugal"] = new[] { "Spain" },
        ["Russia"] = new[] { "Azerbaijan", "Belarus", "China", "Estonia", "Finland", "Georgia", "Kazakhstan", "Latvia", "Lithuania", "Mongolia", "North Korea", "Norway", "Poland", "Ukraine" }, // Lithuania/Poland via Kaliningrad
        ["Serbia"] = new[] { "Bosnia and Herzegovina", "Bulgaria", "Croatia", "Hungary", "Montenegro", "North Macedonia", "Romania" },
        ["South Africa"] = new[] { "Botswana", "Eswatini", "Lesotho", "Mozambique", "Namibia", "Zimbabwe" },
        ["Spain"] = new[] { "Andorra", "France", "Gibraltar", "Morocco", "Portugal" },
        ["Sweden"] = new[] { "Finland", "Norway" },
        ["Switzerland"] = new[] { "Austria", "France", "Germany", "Italy", "Liechtenstein" },
        ["USA"] = new[] { "Canada", "Mexico" },
        ["Ukraine"] = new[] { "Belarus", "Hungary", "Moldova", "Poland", "Romania", "Russia", "Slovakia" },
        ["United Kingdom"] = new[] { "Ireland" }, // Northern Ireland - Republic of Ireland land border
        ["Uruguay"] = new[] { "Argentina", "Brazil" },
        ["Uzbekistan"] = new[] { "Afghanistan", "Kazakhstan", "Kyrgyzstan", "Tajikistan", "Turkmenistan" },
    };

    // Manual exceptions: country pairs with no shared land border that are
    // still intuitively "close" for a geography-guessing game. Deliberately
    // curated by hand -- NOT sourced from the border dataset above -- for
    // island/peninsula nations whose nearest geographic neighbor isn't
    // reachable by land at all.
    private static readonly Dictionary<string, string[]> ManualCloseOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Australia"] = new[] { "New Zealand" },
        ["Japan"] = new[] { "South Korea" },
    };

    // True if the two countries share a real land border, or are linked by
    // the manual override list. Checked symmetrically (order doesn't
    // matter) since a country's own neighbor list might not always be the
    // one recorded here.
    public static bool IsClose(string countryA, string countryB)
    {
        return ContainsPair(Neighbors, countryA, countryB) || ContainsPair(ManualCloseOverrides, countryA, countryB);
    }

    private static bool ContainsPair(Dictionary<string, string[]> map, string countryA, string countryB)
    {
        var aHasB = map.TryGetValue(countryA, out var aNeighbors) && aNeighbors.Contains(countryB, StringComparer.OrdinalIgnoreCase);
        var bHasA = map.TryGetValue(countryB, out var bNeighbors) && bNeighbors.Contains(countryA, StringComparer.OrdinalIgnoreCase);
        return aHasB || bHasA;
    }
}