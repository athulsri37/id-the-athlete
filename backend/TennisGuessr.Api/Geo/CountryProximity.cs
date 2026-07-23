namespace TennisGuessr.Api.Geo;

// Fixed geographic reference data (capital-city coordinates), used only to
// judge "closeness" between a guessed and the actual country for the
// closeness-tier clue feature. Capital cities are used instead of true
// geographic centroids because a centroid can be wildly unrepresentative of
// a large country's practical "location" -- e.g. Canada's geometric center
// sits in the far north near Nunavut, which made Canada register as not
// "close" to the USA despite their shared border. This is static geographic
// fact, not app-specific mutable data like player stats, so it's embedded
// directly in code rather than the database. Covers every country present
// in the Players seed data as of this writing -- add new entries here if a
// future player batch introduces a country not yet listed.
public static class CountryProximity
{
    private static readonly Dictionary<string, (double Lat, double Lon)> Coordinates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Argentina"] = (-34.6037, -58.3816), // Buenos Aires
        ["Australia"] = (-35.2809, 149.1300), // Canberra
        ["Austria"] = (48.2082, 16.3738), // Vienna
        ["Belgium"] = (50.8503, 4.3517), // Brussels
        ["Bosnia and Herzegovina"] = (43.8563, 18.4131), // Sarajevo
        ["Brazil"] = (-15.8267, -47.9218), // Brasília
        ["Bulgaria"] = (42.6977, 23.3219), // Sofia
        ["Canada"] = (45.4215, -75.6972), // Ottawa
        ["Chile"] = (-33.4489, -70.6693), // Santiago
        ["China"] = (39.9042, 116.4074), // Beijing
        ["Croatia"] = (45.8150, 15.9819), // Zagreb
        ["Cyprus"] = (35.1856, 33.3823), // Nicosia
        ["Czech Republic"] = (50.0755, 14.4378), // Prague
        ["Denmark"] = (55.6761, 12.5683), // Copenhagen
        ["Finland"] = (60.1699, 24.9384), // Helsinki
        ["France"] = (48.8566, 2.3522), // Paris
        ["Germany"] = (52.5200, 13.4050), // Berlin
        ["Greece"] = (37.9838, 23.7275), // Athens
        ["Hungary"] = (47.4979, 19.0402), // Budapest
        ["Italy"] = (41.9028, 12.4964), // Rome
        ["Japan"] = (35.6762, 139.6503), // Tokyo
        ["Kazakhstan"] = (51.1694, 71.4491), // Astana
        ["Latvia"] = (56.9496, 24.1052), // Riga
        ["Monaco"] = (43.7384, 7.4246), // Monaco (city-state)
        ["Morocco"] = (34.0209, -6.8416), // Rabat
        ["Netherlands"] = (52.3676, 4.9041), // Amsterdam
        ["Norway"] = (59.9139, 10.7522), // Oslo
        ["Peru"] = (-12.0464, -77.0428), // Lima
        ["Poland"] = (52.2297, 21.0122), // Warsaw
        ["Portugal"] = (38.7223, -9.1393), // Lisbon
        ["Russia"] = (55.7558, 37.6173), // Moscow
        ["Serbia"] = (44.7866, 20.4489), // Belgrade
        ["South Africa"] = (-25.7479, 28.2293), // Pretoria
        ["Spain"] = (40.4168, -3.7038), // Madrid
        ["Sweden"] = (59.3293, 18.0686), // Stockholm
        ["Switzerland"] = (46.9480, 7.4474), // Bern
        ["USA"] = (38.9072, -77.0369), // Washington, D.C.
        ["Ukraine"] = (50.4501, 30.5234), // Kyiv
        ["United Kingdom"] = (51.5074, -0.1278), // London
        ["Uruguay"] = (-34.9011, -56.1645), // Montevideo
        ["Uzbekistan"] = (41.2995, 69.2401), // Tashkent
    };

    // True if both countries are known and their capitals are within
    // thresholdKm of each other (haversine great-circle distance). Unknown
    // countries never count as close.
    public static bool IsWithin(string countryA, string countryB, double thresholdKm)
    {
        if (!Coordinates.TryGetValue(countryA, out var a) || !Coordinates.TryGetValue(countryB, out var b))
            return false;

        return HaversineDistanceKm(a.Lat, a.Lon, b.Lat, b.Lon) < thresholdKm;
    }

    private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}