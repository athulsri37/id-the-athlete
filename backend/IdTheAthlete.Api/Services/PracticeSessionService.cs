using System.Collections.Concurrent;

namespace IdTheAthlete.Api.Services;

// In-memory store for practice-mode sessions (sessionId -> playerId).
// Fine for a small hobby project; would move to a DB table or Redis if
// this needed to survive server restarts / scale horizontally. Registered
// as a Singleton so the dictionary is shared for the app's lifetime (this
// class holds the state itself now, rather than smuggling it via a static
// field the way it lived inside GameService before this refactor).
public class PracticeSessionService
{
    private readonly ConcurrentDictionary<string, int> _sessions = new();

    public string CreateSession(int playerId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = playerId;
        return sessionId;
    }

    public int ResolveSessionPlayerId(string? sessionId)
    {
        if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out var playerId))
            throw new InvalidOperationException("Practice session not found or has expired. Start a new game.");

        return playerId;
    }

    public void RemoveSession(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
