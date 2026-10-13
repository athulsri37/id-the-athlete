namespace IdTheAthlete.Api.Dtos;

public class SportDto
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;      // e.g. "Men's Tennis"
    public string ShortName { get; set; } = string.Empty; // e.g. "Men's"; empty means use Name
    public string Family { get; set; } = string.Empty;    // e.g. "tennis"
    public string UrlName { get; set; } = string.Empty;   // e.g. "men" in /tennis/men; empty means use Slug
    public int MaxGuesses { get; set; }                   // see GameRules
    public int HintAfterGuess { get; set; }
}
