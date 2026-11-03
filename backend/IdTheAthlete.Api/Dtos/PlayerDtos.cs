using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Dtos;

public class PlayerSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GuessRequestDto
{
    public int PlayerId { get; set; }
    public string Mode { get; set; } = "daily"; // "daily" | "easy" | "medium" | "hard"
    public string? SessionId { get; set; }       // client-generated id for practice-mode sessions
    public string? Date { get; set; }             // yyyy-MM-dd; daily mode only, omitted = today
    public string? GameToken { get; set; }        // daily mode only: the token from the previous guess's response; omitted on the first guess
}

public class ClueResultDto
{
    public string AttributeKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "categorical" | "numeric"
    public string Value { get; set; } = string.Empty;
    public bool IsMatch { get; set; }
    public bool IsClose { get; set; } // near-but-not-exact; only meaningful when IsMatch is false
    public string? Direction { get; set; } // "up" | "down" | null (numeric only, null if match)
}

public class GuessResponseDto
{
    public int GuessedPlayerId { get; set; }
    public string GuessedPlayerName { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public List<ClueResultDto> Clues { get; set; } = new();
    public bool GameOver { get; set; }
    public string? AnswerName { get; set; } // populated only when game is over
    public string? TriviaToken { get; set; } // populated only when game is over: pass to the trivia endpoint
    public string? GameToken { get; set; } // daily mode only: send back with the next guess or hint request
    public DateOnly? PuzzleDate { get; set; } // daily mode only: the puzzle this game is for, which can be yesterday's after midnight UTC
    public PlayerPhotoDto? GuessedPlayerPhoto { get; set; } // null when the guessed player has no photo
    public PlayerPhotoDto? AnswerPhoto { get; set; } // game over only, and null when the answer has no photo
}

// A player's photo, a file on Wikimedia Commons that the page shows a
// resized copy of (see Player.PhotoFile), centred on FocusX/FocusY (0-100%).
public class PlayerPhotoDto
{
    public string File { get; set; } = string.Empty;
    public string Credit { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public int FocusX { get; set; } = 50;
    public int FocusY { get; set; } = 50;

    // Null when the player has no photo.
    public static PlayerPhotoDto? From(Player player) => player.PhotoFile is null ? null : new PlayerPhotoDto
    {
        File = player.PhotoFile,
        Credit = player.PhotoCredit ?? "",
        License = player.PhotoLicense ?? "",
        FocusX = player.PhotoFocusX,
        FocusY = player.PhotoFocusY,
    };
}

// One photo on the Photo credits page: whose it is, and its author and licence.
public class PhotoCreditDto
{
    public string SportSlug { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Credit { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
}

public class StartGameResponseDto
{
    public string Mode { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int MaxGuesses { get; set; }
}

public class TriviaDto
{
    public string? TriviaBlurb { get; set; } // null when trivia is unavailable
}

public class CountryHintDto
{
    public string Country { get; set; } = string.Empty;
}

public class AttributeDefinitionDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
