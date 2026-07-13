namespace TennisGuessr.Api.Models;

public class DailyPuzzle
{
    public int Id { get; set; }
    public int SportId { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }
    public DateOnly PuzzleDate { get; set; }
}
