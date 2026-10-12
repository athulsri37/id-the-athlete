using System.Collections.Concurrent;

namespace IdTheAthlete.Api.Services;

// In-memory store for practice-mode sessions (sessionId -> playerId and
// guesses made so far). Fine for a small hobby project; would move to a DB
// table or Redis if this needed to survive server restarts / scale
// horizontally. Registered as a Singleton so the dictionary is shared for
// the app's lifetime (this class holds the state itself now, rather than
// smuggling it via a static field the way it lived inside GameService
// before this refactor).
//
// The guess count lives here, not with the client, so a client can't claim
// to be on its last guess to get the answer revealed early.
public class PracticeSessionService : IPracticeSessionService
{
    private sealed class PracticeSession
    {
        public required int PlayerId { get; init; }
        public int GuessCount;
    }

    private readonly ConcurrentDictionary<string, PracticeSession> _sessions = new();

    public string CreateSession(int playerId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = new PracticeSession { PlayerId = playerId };
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

    private PracticeSession GetSession(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("Practice session not found or has expired. Start a new game.");

        return session;
    }
}
