using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using IdTheAthlete.Api.Errors;

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
// The key comes from GameTokenOptions, already checked at startup by
// GameTokenOptionsValidator. Changing the key invalidates every in-progress
// daily game, which is harmless: the player just starts over. Registered as
// a Singleton.
//
// The same format also carries trivia tokens (see TriviaGrant), signed with
// a separate key derived from the configured one, so neither kind of token
// is ever accepted as the other.
public sealed class GameTokenService : IGameTokenService
{
    private readonly byte[] _key;
    private readonly byte[] _triviaKey;

    public GameTokenService(IOptions<GameTokenOptions> options, ILogger<GameTokenService> logger)
    {
        var configuredKey = options.Value.SigningKey;

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            // The validator only lets an empty key through in Development.
            // Use a random key so the app runs without extra setup; games in
            // progress just don't survive a restart.
            logger.LogWarning(
                "{Section}:SigningKey is not set; using a random key for this run. In-progress daily games will not survive a restart.",
                GameTokenOptions.SectionName);
            _key = RandomNumberGenerator.GetBytes(GameTokenOptions.MinimumKeyLength);
        }
        else
        {
            _key = Encoding.UTF8.GetBytes(configuredKey);
        }

        _triviaKey = HMACSHA256.HashData(_key, "trivia-token"u8);
    }

    public string Issue(DailyGameState state) => Sign(_key, state);

    public DailyGameState Read(string token) => Verify<DailyGameState>(_key, token);

    public string IssueTrivia(TriviaGrant grant) => Sign(_triviaKey, grant);

    public TriviaGrant ReadTrivia(string token) => Verify<TriviaGrant>(_triviaKey, token);

    private static string Sign<T>(byte[] key, T value)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        var signature = HMACSHA256.HashData(key, payload);
        return $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(signature)}";
    }

    // Throws InvalidRequestException (a 400) for anything this server didn't
    // sign exactly as-is with this key, like every other bad game request.
    private static T Verify<T>(byte[] key, string token) where T : class
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

        var expected = HMACSHA256.HashData(key, payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
            throw InvalidToken();

        // A correctly signed payload was written by Sign above, so this can
        // only fail if the payload format changes between deploys.
        return JsonSerializer.Deserialize<T>(payload) ?? throw InvalidToken();
    }

    private static InvalidRequestException InvalidToken() =>
        new("This game could not be verified. Start the game again.");
}
