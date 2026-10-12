using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdTheAthlete.Api.Services;

// Issues and verifies the signed token that carries a daily game's guess
// count between requests. Daily mode has no server-side session (player
// progress lives in the browser), so before this the client simply told the
// server which guess it was on -- and claiming guess 8 revealed the answer
// with no real guesses at all. Now the count only ever comes from a token
// this server signed, so the answer is revealed after 8 real guesses and no
// sooner.
//
// Format: base64url(JSON payload) + "." + base64url(HMAC-SHA256 of payload).
// The key comes from GameTokens:SigningKey (user-secrets locally, an
// environment variable in production). Changing the key invalidates every
// in-progress daily game, which is harmless: the player just starts over.
// Registered as a Singleton.
public sealed class GameTokenService : IGameTokenService
{
    public const string SigningKeyConfigKey = "GameTokens:SigningKey";
    private const int MinimumKeyLength = 32;

    private readonly byte[] _key;

    public GameTokenService(IConfiguration configuration, IHostEnvironment environment, ILogger<GameTokenService> logger)
    {
        var configuredKey = configuration[SigningKeyConfigKey];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            // Outside Development a missing key is a deployment mistake, so
            // fail at startup (Program.cs resolves this service eagerly)
            // rather than on the first guess. In Development, fall back to a
            // random key so the app still runs without extra setup; games
            // in progress just don't survive a restart.
            if (!environment.IsDevelopment())
                throw new InvalidOperationException(
                    $"{SigningKeyConfigKey} is not set. Set it to a random string of at least {MinimumKeyLength} characters " +
                    "(environment variable GameTokens__SigningKey).");

            logger.LogWarning(
                "{ConfigKey} is not set; using a random key for this run. In-progress daily games will not survive a restart.",
                SigningKeyConfigKey);
            _key = RandomNumberGenerator.GetBytes(MinimumKeyLength);
            return;
        }

        if (configuredKey.Length < MinimumKeyLength)
            throw new InvalidOperationException(
                $"{SigningKeyConfigKey} must be at least {MinimumKeyLength} characters long.");

        _key = Encoding.UTF8.GetBytes(configuredKey);
    }

    public string Issue(DailyGameState state)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(state);
        var signature = HMACSHA256.HashData(_key, payload);
        return $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(signature)}";
    }

    // Throws InvalidOperationException for anything this server didn't sign
    // exactly as-is, which the controller turns into a 400 like every other
    // bad game request.
    public DailyGameState Read(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2)
            throw InvalidToken();

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Base64Url.DecodeFromChars(parts[0]);
            signature = Base64Url.DecodeFromChars(parts[1]);
        }
        catch (FormatException)
        {
            throw InvalidToken();
        }

        var expected = HMACSHA256.HashData(_key, payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
            throw InvalidToken();

        // A correctly signed payload was written by Issue above, so this can
        // only fail if the payload format changes between deploys.
        return JsonSerializer.Deserialize<DailyGameState>(payload) ?? throw InvalidToken();
    }

    private static InvalidOperationException InvalidToken() =>
        new("This game could not be verified. Start the game again.");
}
