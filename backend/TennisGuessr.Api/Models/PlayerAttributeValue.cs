namespace TennisGuessr.Api.Models;

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
}
