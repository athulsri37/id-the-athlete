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
    public string GuessedPlayerName { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public List<ClueResultDto> Clues { get; set; } = new();
    public bool GameOver { get; set; }
    public string? AnswerName { get; set; } // populated only when game is over
    public string? TriviaToken { get; set; } // populated only when game is over: pass to the trivia endpoint
    public string? GameToken { get; set; } // daily mode only: send back with the next guess or hint request
    public DateOnly? PuzzleDate { get; set; } // daily mode only: the puzzle this game is for, which can be yesterday's after midnight UTC
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
