using System.Globalization;

namespace IdTheAthlete.Api.Services;

// A set of settings read together in one query by
// ISettingsService.GetBatchAsync, or their built-in fallbacks if that read
// failed. Read the same way as the single-setting reads: a flag is on only
// when its value is exactly "true", and a decimal is parsed with the
// invariant culture. A missing or unparseable value counts as off or not
// configured, never an error.
public sealed class SettingsBatch(IReadOnlyDictionary<string, string> values)
{
    public bool IsEnabled(string key) => values.TryGetValue(key, out var value) && value == "true";

    public bool TryGetDecimal(string key, out decimal value)
    {
        value = 0;
        return values.TryGetValue(key, out var raw)
            && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }
}
