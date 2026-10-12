namespace IdTheAthlete.Api.Models;

public class Sport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Tennis"
    public string Slug { get; set; } = string.Empty; // e.g. "tennis"

    // Which ISportRules implementation scores this sport, e.g. "tennis" or
    // "cricket" (see Rules/SportFamilies). Several tours share a family:
    // Men's and Women's Tennis are both "tennis".
    public string Family { get; set; } = string.Empty;

    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<AttributeDefinition> Attributes { get; set; } = new List<AttributeDefinition>();
}
