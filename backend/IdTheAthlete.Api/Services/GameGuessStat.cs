namespace IdTheAthlete.Api.Services;

// What one valid guess tells the stats about its game (see GameStatsService).
// Mode is "daily" or the practice difficulty the server started the game
// with; PuzzleDate is set for daily games only.
public sealed record GameGuessStat(
    Guid GameId,
    int SportId,
    string Mode,
    DateOnly? PuzzleDate,
    int MysteryPlayerId,
    int GuessNumber,
    bool GameOver,
    bool Won);
