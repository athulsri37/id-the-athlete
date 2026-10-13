namespace IdTheAthlete.Api.Services;

// The game's numbers, in one place: GameService enforces them, and
// GET /api/sports hands them to the frontend, so the page never keeps its
// own copy that could drift from what the server enforces.
public static class GameRules
{
    // Guesses per game; the answer is revealed after the last one.
    public const int MaxGuesses = 8;

    // The country hint unlocks once this many guesses have been made.
    public const int HintAfterGuess = 5;
}
