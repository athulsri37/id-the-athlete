namespace TennisGuessr.Api.Models;

public enum AttributeType
{
    Categorical, // exact match = green, else gray (e.g. Country, Plays)
    Numeric      // supports up/down arrow comparison (e.g. Grand Slam titles)
}

public class AttributeDefinition
{
    public int Id { get; set; }
    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Key { get; set; } = string.Empty;   // e.g. "grand_slam_titles"
    public string Label { get; set; } = string.Empty; // e.g. "Grand Slams"
    public AttributeType Type { get; set; }
    public int DisplayOrder { get; set; }

    public ICollection<PlayerAttributeValue> Values { get; set; } = new List<PlayerAttributeValue>();
}
