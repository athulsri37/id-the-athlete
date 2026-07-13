namespace TennisGuessr.Api.Models;

public class Player
{
    public int Id { get; set; }
    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public string Name { get; set; } = string.Empty;
    public string EraGroup { get; set; } = string.Empty; // "current", "2005_2015", "legend"

    public ICollection<PlayerAttributeValue> AttributeValues { get; set; } = new List<PlayerAttributeValue>();
}
