namespace IdTheAthlete.Api.Services;

public interface IPracticeSessionService
{
    string CreateSession(int playerId, string difficulty);
    PracticeGame ResolveSession(string? sessionId);
    int RecordGuess(string? sessionId);
    int GetGuessCount(string? sessionId);
    void RemoveSession(string sessionId);
}
