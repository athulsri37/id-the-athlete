namespace TennisGuessr.Api.Models;

public class Player
{
    public int Id { get; set; }
    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Name { get; set; } = string.Empty;

    // Difficulty is normally computed from stats (see GameService), but a
    // curator can override it for a specific player when raw stats don't
    // reflect real-world recognizability (e.g. a well-known player whose
    // title count alone would compute too hard).
    public string? DifficultyOverride { get; set; }
    public bool IsOverridden { get; set; } = false;

    public ICollection<PlayerAttributeValue> AttributeValues { get; set; } = new List<PlayerAttributeValue>();
}
