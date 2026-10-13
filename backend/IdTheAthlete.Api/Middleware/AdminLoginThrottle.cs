using System.Collections.Concurrent;

namespace IdTheAthlete.Api.Middleware;

// Locks a client address out of the admin API after MaxFailures wrong (or
// missing) admin keys within Window, until the oldest of those failures is
// Window old. While locked out every request is refused, the right key
// included: that's what stops a key being guessed by trying many. A right
// key clears the address's failures. In memory, per server; a restart
// forgets the failures, which only helps whoever was locked out.
//
// Behind a proxy every request seems to come from the proxy, so locking one
// address would lock everyone out: deployments behind one need forwarded
// headers enabled (README, "Admin Tool"). Registered as a Singleton.
public sealed class AdminLoginThrottle : IAdminLoginThrottle
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _failures = new();
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _nextSweep;

    public AdminLoginThrottle(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _nextSweep = timeProvider.GetUtcNow() + Window;
    }

    public TimeSpan? LockedOutFor(string client)
    {
        if (!_failures.TryGetValue(client, out var failures))
            return null;

        var now = _timeProvider.GetUtcNow();
        lock (failures)
        {
            failures.RemoveAll(at => now - at >= Window);
            return failures.Count >= MaxFailures ? failures[0] + Window - now : null;
        }
    }

    public void RecordFailure(string client)
    {
        var now = _timeProvider.GetUtcNow();
        var failures = _failures.GetOrAdd(client, _ => new List<DateTimeOffset>());
        lock (failures)
        {
            failures.RemoveAll(at => now - at >= Window);
            failures.Add(now);
        }
        SweepIfDue(now);
    }

    public void RecordSuccess(string client) => _failures.TryRemove(client, out _);

    // Forgets addresses whose failures have all expired, at most once per
    // Window, so addresses that tried once and left don't pile up.
    private void SweepIfDue(DateTimeOffset now)
    {
        if (now < _nextSweep)
            return;
        _nextSweep = now + Window;

        foreach (var (client, failures) in _failures)
        {
            bool expired;
            lock (failures)
                expired = failures.All(at => now - at >= Window);
            if (expired)
                _failures.TryRemove(client, out _);
        }
    }

    // Addresses currently tracked. For tests.
    internal int TrackedClients => _failures.Count;
}
