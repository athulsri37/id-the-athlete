namespace IdTheAthlete.Api.Models;

// Stores every attribute value as text; numeric attributes are parsed
// to int/decimal by the comparison service when needed. This keeps the
// schema generic across sports without needing a column per attribute type.
public class PlayerAttributeValue
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public Player? Player { get; set; }

    public int AttributeDefinitionId { get; set; }
    public AttributeDefinition? AttributeDefinition { get; set; }

    public string Value { get; set; } = string.Empty; // e.g. "Right", "20", "Spain"

    // Set by AdminService.UpdatePlayerAsync whenever a curator edits this
    // value via /control-room. Every seed file's upsert checks this before
    // overwriting Value on re-run (see SeedData/**/*.sql), so a manual
    // correction survives re-seeding indefinitely instead of silently
    // reverting to whatever the original batch file hardcoded.
    public bool IsManuallyEdited { get; set; } = false;
}
