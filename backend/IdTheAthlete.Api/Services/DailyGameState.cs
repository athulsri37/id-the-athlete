namespace IdTheAthlete.Api.Services;

// What a daily-game token proves about the game in progress. Signed but not
// encrypted -- anyone can decode it -- so it must never carry the answer.
public sealed record DailyGameState(int SportId, DateOnly PuzzleDate, int GuessCount);
