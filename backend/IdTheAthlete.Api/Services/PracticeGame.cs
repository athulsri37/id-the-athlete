namespace IdTheAthlete.Api.Services;

// What the server knows about a practice game in progress: the answer, the
// difficulty it was started with (stats record this, never the mode a
// request claims), and its random stats ID (see GameRecord).
public sealed record PracticeGame(int PlayerId, string Difficulty, Guid GameId);
