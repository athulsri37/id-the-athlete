namespace IdTheAthlete.Api.Models;

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

    // Set whenever a curator edits any attribute value for this player via
    // the admin tool (see AdminService.UpdatePlayerAsync). Purely a
    // "when was this player last touched" signal for staleness checks
    // elsewhere (e.g. AiTriviaService) -- never written by seed files, so
    // it needs none of the upsert-protection logic IsManuallyEdited has.
    public DateTime? LastModifiedAt { get; set; }

    public ICollection<PlayerAttributeValue> AttributeValues { get; set; } = new List<PlayerAttributeValue>();
}
