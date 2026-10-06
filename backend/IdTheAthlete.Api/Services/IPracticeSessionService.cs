namespace IdTheAthlete.Api.Services;

public interface IPracticeSessionService
{
    string CreateSession(int playerId);
    int ResolveSessionPlayerId(string? sessionId);
    void RemoveSession(string sessionId);
}
