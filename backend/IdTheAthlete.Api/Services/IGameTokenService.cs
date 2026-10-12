namespace IdTheAthlete.Api.Services;

public interface IGameTokenService
{
    string Issue(DailyGameState state);
    DailyGameState Read(string token);
}

// What a daily-game token proves about the game in progress. Signed but not
// encrypted -- anyone can decode it -- so it must never carry the answer.
public sealed record DailyGameState(int SportId, DateOnly PuzzleDate, int GuessCount);
