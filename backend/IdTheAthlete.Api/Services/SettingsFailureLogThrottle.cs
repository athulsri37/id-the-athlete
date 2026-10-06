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

    private sealed class KeyState
    {
        public DateTimeOffset LastLogged;
        public int Suppressed;
    }

    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, KeyState> _keys = new();
    private readonly object _gate = new();

    public SettingsFailureLogThrottle(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    // Call once per failure. True if this failure should be logged now, in
    // which case suppressedSinceLastLog is how many failures of this key went
    // unlogged since its previous log line (0 the first time) and the count
    // starts again from zero. False while still inside the window, in which
    // case this failure is added to that count.
    public bool ShouldLog(string key, out int suppressedSinceLastLog)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_keys.TryGetValue(key, out var state) && now - state.LastLogged < Window)
            {
                state.Suppressed++;
                suppressedSinceLastLog = 0;
                return false;
            }

            suppressedSinceLastLog = state?.Suppressed ?? 0;
            _keys[key] = new KeyState { LastLogged = now };
            return true;
        }
    }

    // Appended to a log message: nothing when no failures were suppressed,
    // otherwise e.g. " (3 failures suppressed since last log)".
    public static string SuppressedNote(int suppressed) => suppressed switch
    {
        0 => "",
        1 => " (1 failure suppressed since last log)",
        _ => $" ({suppressed} failures suppressed since last log)",
    };
}
