namespace IdTheAthlete.Api.Services;

// Limits how often SettingsService logs a failed read of the same settings
// key, so a sustained outage produces a steady trickle of log lines instead
// of one per request. Only logging is affected; fallback values are returned
// on every failure regardless. SettingsService is Scoped, so this state lives
// in a Singleton to survive across requests.
public sealed class SettingsFailureLogThrottle
{
    // Hardcoded on purpose: reading it from AppSettings would make the
    // settings-failure log depend on the very table whose failure it reports.
    // Each key is throttled separately, so a key that starts failing always
    // logs at once; 60s just caps repeats for a key that keeps failing at
    // roughly one line a minute, while still showing within a minute that an
    // outage is ongoing.
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, DateTimeOffset> _lastLogged = new();
    private readonly object _gate = new();

    public SettingsFailureLogThrottle(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    // True if a failure for this key should be logged now, in which case the
    // current time is recorded as its last log; false while still inside the
    // window since the key was last logged.
    public bool ShouldLog(string key)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_lastLogged.TryGetValue(key, out var last) && now - last < Window)
                return false;

            _lastLogged[key] = now;
            return true;
        }
    }
}
