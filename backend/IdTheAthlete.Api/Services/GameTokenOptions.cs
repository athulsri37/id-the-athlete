namespace IdTheAthlete.Api.Services;

// Bound from the "GameTokens" configuration section (user-secrets locally,
// the GameTokens__SigningKey environment variable in production) and
// checked at startup by GameTokenOptionsValidator.
public sealed class GameTokenOptions
{
    public const string SectionName = "GameTokens";
    public const int MinimumKeyLength = 32;

    public string? SigningKey { get; set; }
}
