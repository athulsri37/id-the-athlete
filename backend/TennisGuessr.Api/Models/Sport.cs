namespace TennisGuessr.Api.Models;

public class Sport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Tennis"
    public string Slug { get; set; } = string.Empty; // e.g. "tennis"

    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<AttributeDefinition> Attributes { get; set; } = new List<AttributeDefinition>();
}
