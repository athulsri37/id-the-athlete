using System.Globalization;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;

namespace IdTheAthlete.Api.Services;

// The single place that reads and writes the AppSettings table. Every read
// goes to the database on every call -- deliberately no caching -- so a
// setting changed through the admin tool takes effect on the very next
// request (the closeness evaluators rely on this). Reads are projections,
// so a row tracked earlier in the same request can never mask a newer value.
// Registered Scoped (depends on GameDbContext).
public class SettingsService : ISettingsService
{
    private const string ThemeKey = "ActiveTheme";
    private const string DefaultTheme = "retro";

    // Used only when the numeric settings query itself fails, so a database
    // problem doesn't fail a live guess. These match the values in the
    // database and in SeedData/00-app-settings.sql when this was written;
    // retune them together, or closeness judged during an outage will drift
    // from closeness judged normally.
    private static readonly Dictionary<string, decimal> NumericFallbacks = new()
    {
        ["CricketMatchesClosenessPercent"] = 15m,
        ["CricketMatchesClosenessFloor"] = 20m,
        ["CricketRunsClosenessPercent"] = 15m,
        ["CricketRunsClosenessFloor"] = 500m,
        ["CricketWicketsClosenessPercent"] = 15m,
        ["CricketWicketsClosenessFloor"] = 15m,
    };

    private readonly GameDbContext _db;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(GameDbContext db, ILogger<SettingsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // On only when the stored value is exactly "true". A missing row, any
    // other value, or a failed read all count as off.
    public async Task<bool> IsEnabledAsync(string key)
    {
        try
        {
            var value = await _db.AppSettings
                .Where(s => s.Key == key)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();

            return value == "true";
        }
        catch
        {
            return false;
        }
    }

    // One round trip for the whole batch. Missing or unparseable values are
    // left out of the result rather than throwing -- callers treat an absent
    // key as "not configured", not an error. Parsed with the invariant
    // culture so "2.5" means 2.5 whatever locale the server runs in.
    //
    // If the query fails, returns NumericFallbacks for the requested keys
    // instead of throwing, and logs the failure. A requested key with no
    // fallback is left out, the same as a missing setting: no closeness tier
    // for that attribute is safer than a guessed threshold that looks right.
    public async Task<Dictionary<string, decimal>> GetDecimalsAsync(IEnumerable<string> keys)
    {
        var keyList = keys.ToList();
        List<KeyValuePair<string, string>> rows;
        try
        {
            rows = await _db.AppSettings
                .Where(s => keyList.Contains(s.Key))
                .Select(s => new KeyValuePair<string, string>(s.Key, s.Value))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            var requested = keyList.Distinct().ToList();
            var fallback = requested
                .Where(NumericFallbacks.ContainsKey)
                .ToDictionary(k => k, k => NumericFallbacks[k]);
            var withoutFallback = requested.Where(k => !NumericFallbacks.ContainsKey(k)).ToList();

            _logger.LogError(ex,
                "Reading numeric settings failed; using built-in fallback values for {FallbackKeys}" +
                " and treating {KeysWithoutFallback} as not configured.",
                fallback.Keys, withoutFallback);
            return fallback;
        }

        var result = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            if (decimal.TryParse(row.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                result[row.Key] = parsed;
        }
        return result;
    }

    public async Task<string> GetThemeAsync()
    {
        return await _db.AppSettings
            .Where(s => s.Key == ThemeKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync() ?? DefaultTheme;
    }

    public async Task<List<KeyValuePair<string, string>>> GetAllAsync()
    {
        return await _db.AppSettings
            .OrderBy(s => s.Key)
            .Select(s => new KeyValuePair<string, string>(s.Key, s.Value))
            .ToListAsync();
    }

    // A setting whose current value is exactly "true"/"false" only accepts
    // "true"/"false". This is re-validated here because nothing stops a raw
    // request from bypassing the admin UI's True/False dropdown.
    public async Task UpdateAsync(string key, string newValue)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(s => s.Key == key)
            ?? throw new InvalidOperationException($"Setting '{key}' not found.");

        var wasBoolean = setting.Value == "true" || setting.Value == "false";
        if (wasBoolean && newValue != "true" && newValue != "false")
            throw new InvalidOperationException($"Setting '{key}' is boolean; value must be 'true' or 'false'.");

        setting.Value = newValue;
        await _db.SaveChangesAsync();
    }
}
