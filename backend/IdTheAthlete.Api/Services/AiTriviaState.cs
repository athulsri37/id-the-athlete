using System.Collections.Concurrent;

namespace IdTheAthlete.Api.Services;

// The trivia cache and rate-limit circuit breaker shared by every
// AiTriviaService instance. AiTriviaService is created fresh per use (it's a
// typed HttpClient), so this state has to outlive any one instance. It used
// to live in static fields; as a registered singleton it is still shared
// app-wide, but each DI container (e.g. each test) gets its own copy, and
// "now" is passed in from the caller's TimeProvider rather than read here.
public sealed class AiTriviaState
{
    private readonly object _rateLimitGate = new();
    private DateTimeOffset _rateLimitedUntil = DateTimeOffset.MinValue;

    // Only ever written on a successful generation, so a failed attempt never
    // poisons the cache with a permanent null.
    public ConcurrentDictionary<int, CachedTriviaBlurb> Cache { get; } = new();

    public bool IsCircuitOpen(DateTimeOffset now)
    {
        lock (_rateLimitGate)
        {
            return now < _rateLimitedUntil;
        }
    }

    public void OpenCircuitUntil(DateTimeOffset until)
    {
        lock (_rateLimitGate)
        {
            _rateLimitedUntil = until;
        }
    }

    public void ClearCircuit()
    {
        lock (_rateLimitGate)
        {
            _rateLimitedUntil = DateTimeOffset.MinValue;
        }
    }
}

// Cached alongside the moment it was generated so a lookup can tell a
// still-fresh blurb apart from one whose player was edited afterward.
public sealed record CachedTriviaBlurb(string Blurb, DateTime CachedAt);
