namespace TennisGuessr.Api.Dtos;

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
}

public class ClueResultDto
{
    public string AttributeKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "categorical" | "numeric"
    public string Value { get; set; } = string.Empty;
    public bool IsMatch { get; set; }
    public string? Direction { get; set; } // "up" | "down" | null (numeric only, null if match)
}

public class GuessResponseDto
{
    public string GuessedPlayerName { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public List<ClueResultDto> Clues { get; set; } = new();
    public bool GameOver { get; set; }
    public string? AnswerName { get; set; } // populated only when game is over
}

public class StartGameResponseDto
{
    public string Mode { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public int MaxGuesses { get; set; }
}
