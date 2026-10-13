namespace IdTheAthlete.Api.Dtos;

// The admin Stats tab: the README's four "Game Stats" views over the games
// started in the requested range (see AdminStatsService).
public class AdminStatsDto
{
    public List<AdminStatsOutcomeRow> Outcomes { get; set; } = new();
    public List<AdminStatsWinningGuessRow> WinningGuesses { get; set; } = new();
    public List<AdminStatsHintRow> Hints { get; set; } = new();
    public List<AdminStatsPuzzleRow> Puzzles { get; set; } = new();
}

// How games end, per tour and mode.
public class AdminStatsOutcomeRow
{
    public string Tour { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty; // daily | past challenge | easy | medium | hard
    public int Games { get; set; }
    public int Won { get; set; }
    public int Lost { get; set; }
    public int Abandoned { get; set; }
    public int InProgress { get; set; }
    public decimal FinishedPercent { get; set; }
}

// Which guess people win on.
public class AdminStatsWinningGuessRow
{
    public string Tour { get; set; } = string.Empty;
    public int Guess { get; set; }
    public int Games { get; set; }
}

// The hint, over finished games that reached it.
public class AdminStatsHintRow
{
    public string Tour { get; set; } = string.Empty;
    public int ReachedHint { get; set; }
    public int TookHint { get; set; }
    public decimal? WinPercentWithHint { get; set; }    // null when no game took it
    public decimal? WinPercentWithoutHint { get; set; } // null when every game took it
}

// How hard each daily puzzle was (Past Challenge plays included).
public class AdminStatsPuzzleRow
{
    public DateOnly PuzzleDate { get; set; }
    public string Tour { get; set; } = string.Empty;
    public string Athlete { get; set; } = string.Empty;
    public int Games { get; set; }
    public decimal WinPercent { get; set; }
    public decimal? AverageWinningGuess { get; set; } // null when nobody won
}
