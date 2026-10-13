namespace IdTheAthlete.Api.Models;

// One row per game played, for anonymous gameplay stats (completion rate,
// winning guess, hint use). Nothing here identifies a person: Id is random
// per game, carried in the daily token or the practice session, and there
// is deliberately no IP address, device ID or cookie anywhere in it.
//
// A game is recorded from its first guess. An unfinished game (Result null)
// counts as abandoned once LastActivityAt is 90 minutes old; that's decided
// when the stats are read, not stored.
public class GameRecord
{
    public Guid Id { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    // "daily", or the practice difficulty: "easy", "medium" or "hard".
    public string Mode { get; set; } = string.Empty;

    // Daily games only: the puzzle's date, and whether it was already a past
    // date when the game started (played from Past Challenges).
    public DateOnly? PuzzleDate { get; set; }
    public bool IsPastChallenge { get; set; }

    // Null only if the athlete is later deleted; the stats row stays.
    public int? MysteryPlayerId { get; set; }
    public Player? MysteryPlayer { get; set; }

    public int GuessCount { get; set; }

    // The guess count when the country hint was first taken; null if never.
    public int? HintTakenAtGuess { get; set; }

    // Null while unfinished.
    public GameResult? Result { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
