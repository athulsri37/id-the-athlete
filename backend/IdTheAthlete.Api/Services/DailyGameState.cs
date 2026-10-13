namespace IdTheAthlete.Api.Services;

// What a daily-game token proves about the game in progress. Signed but not
// encrypted -- anyone can decode it -- so it must never carry the answer.
// GameId is random per game and ties its guesses together in the stats
// (see GameRecord); tokens issued before it existed read as Guid.Empty and
// simply aren't recorded.
public sealed record DailyGameState(int SportId, DateOnly PuzzleDate, int GuessCount, Guid GameId);
