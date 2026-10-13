namespace IdTheAthlete.Api.Models;

public class Sport
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // e.g. "Men's Tennis"
    public string Slug { get; set; } = string.Empty; // e.g. "tennis-men"

    // How the tour is labelled among its family's other tours, e.g. "Men's"
    // on the Tennis tour-select screen. Empty means "use Name".
    public string ShortName { get; set; } = string.Empty;

    // The tour's part of the game's web addresses, e.g. "men" in /tennis/men,
    // unique within its family. Empty means the frontend uses Slug instead.
    public string UrlName { get; set; } = string.Empty;

    // Which ISportRules implementation scores this sport, e.g. "tennis" or
    // "cricket" (see Rules/SportFamilies). Several tours share a family:
    // Men's and Women's Tennis are both "tennis".
    public string Family { get; set; } = string.Empty;

    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<AttributeDefinition> Attributes { get; set; } = new List<AttributeDefinition>();
}
