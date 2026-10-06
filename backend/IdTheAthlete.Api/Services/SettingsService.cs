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

    private readonly GameDbContext _db;

    public SettingsService(GameDbContext db)
    {
        _db = db;
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
    // key as "not configured", not an error.
    public async Task<Dictionary<string, decimal>> GetDecimalsAsync(IEnumerable<string> keys)
    {
        var keyList = keys.ToList();
        var rows = await _db.AppSettings
            .Where(s => keyList.Contains(s.Key))
            .Select(s => new { s.Key, s.Value })
            .ToListAsync();

        var result = new Dictionary<string, decimal>();
        foreach (var row in rows)
        {
            if (decimal.TryParse(row.Value, out var parsed))
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
