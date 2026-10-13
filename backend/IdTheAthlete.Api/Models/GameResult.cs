namespace IdTheAthlete.Api.Models;

// How a finished game ended. Stored as text, so the stats read naturally in
// SQL. An abandoned game has no result (see GameRecord).
public enum GameResult
{
    Won,
    Lost
}
