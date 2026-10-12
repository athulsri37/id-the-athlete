using Microsoft.Extensions.Options;

namespace IdTheAthlete.Api.Services;

// Runs at startup (ValidateOnStart in Program.cs), so a deployment without a
// usable signing key stops immediately instead of failing the first daily
// guess. A missing key is allowed only in Development, where
// GameTokenService falls back to a random per-run key.
public sealed class GameTokenOptionsValidator : IValidateOptions<GameTokenOptions>
{
    private readonly IHostEnvironment _environment;

    public GameTokenOptionsValidator(IHostEnvironment environment)
    {
        _environment = environment;
    }

    public ValidateOptionsResult Validate(string? name, GameTokenOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return _environment.IsDevelopment()
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail(
                    $"{GameTokenOptions.SectionName}:SigningKey is not set. Set it to a random string of at least " +
                    $"{GameTokenOptions.MinimumKeyLength} characters (environment variable GameTokens__SigningKey).");
        }

        return options.SigningKey.Length >= GameTokenOptions.MinimumKeyLength
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{GameTokenOptions.SectionName}:SigningKey must be at least {GameTokenOptions.MinimumKeyLength} characters long.");
    }
}
