using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using IdTheAthlete.Api.Errors;

namespace IdTheAthlete.Api.Services;

// In-memory store for practice-mode sessions (sessionId -> playerId and
// guesses made so far). Sessions don't survive a restart and aren't shared
// between servers, so a deploy ends practice games in progress; the player
// is then told to start a new game, as when one expires.
// Registered as a Singleton so the dictionary is shared for the app's
// lifetime.
//
// The guess count lives here, not with the client, so a client can't claim
// to be on its last guess to get the answer revealed early.
//
// A finished game is removed straight away. Any other session is removed
// once it has gone PracticeSessionOptions.IdleTimeout without a guess or
// hint: a request for it is then refused like an unknown session, and
// CreateSession sweeps out the rest. Sessions are only ever added there, so
// sweeping there keeps the store bounded without a background job.
public sealed class PracticeSessionService : IPracticeSessionService
{
    // How often CreateSession scans for idle sessions. A session can
    // outlive its timeout by up to this long before it's freed, but it's
    // refused as soon as the timeout passes.
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private sealed class PracticeSession
    {
        public required int PlayerId { get; init; }
        public int GuessCount;
        // UTC ticks, read and written with Interlocked so concurrent
        // requests for the same session never see a torn value.
        public long LastActiveTicks;
    }

    private readonly ConcurrentDictionary<string, PracticeSession> _sessions = new();
    private readonly TimeSpan _idleTimeout;
    private readonly TimeProvider _timeProvider;
    private long _nextSweepTicks;

    public PracticeSessionService(IOptions<PracticeSessionOptions> options, TimeProvider timeProvider)
    {
        _idleTimeout = options.Value.IdleTimeout;
        _timeProvider = timeProvider;
        _nextSweepTicks = NowTicks() + SweepInterval.Ticks;
    }

    public string CreateSession(int playerId)
    {
        var now = NowTicks();
        SweepIfDue(now);

        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = new PracticeSession { PlayerId = playerId, LastActiveTicks = now };
        return sessionId;
    }

    public int ResolveSessionPlayerId(string? sessionId)
    {
        return GetSession(sessionId).PlayerId;
    }

    // Counts one more guess against this session and returns the new total.
    public int RecordGuess(string? sessionId)
    {
        var session = GetSession(sessionId);
        return Interlocked.Increment(ref session.GuessCount);
    }

    public int GetGuessCount(string? sessionId)
    {
        return Volatile.Read(ref GetSession(sessionId).GuessCount);
    }

    public void RemoveSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }

    // Sessions still held in memory, idle or not. For tests.
    internal int StoredSessionCount => _sessions.Count;

    // Every guess and hint goes through here, so this is what keeps an
    // active game alive.
    private PracticeSession GetSession(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out var session))
            throw SessionNotFound();

        var now = NowTicks();
        if (IsIdle(session, now))
        {
            _sessions.TryRemove(sessionId, out _);
            throw SessionNotFound();
        }

        Interlocked.Exchange(ref session.LastActiveTicks, now);
        return session;
    }

    private void SweepIfDue(long now)
    {
        var due = Interlocked.Read(ref _nextSweepTicks);
        // Only the request that moves the next sweep time forward does the
        // sweep, so concurrent CreateSession calls never scan twice.
        if (now < due || Interlocked.CompareExchange(ref _nextSweepTicks, now + SweepInterval.Ticks, due) != due)
            return;

        // Safe alongside requests in flight: a session that's idle now is
        // refused by GetSession anyway, so removing it never ends a live game.
        foreach (var (sessionId, session) in _sessions)
        {
            if (IsIdle(session, now))
                _sessions.TryRemove(sessionId, out _);
        }
    }

    private bool IsIdle(PracticeSession session, long now) =>
        now - Interlocked.Read(ref session.LastActiveTicks) > _idleTimeout.Ticks;

    private long NowTicks() => _timeProvider.GetUtcNow().UtcTicks;

    private static InvalidRequestException SessionNotFound() =>
        new("Practice session not found or has expired. Start a new game.");
}
