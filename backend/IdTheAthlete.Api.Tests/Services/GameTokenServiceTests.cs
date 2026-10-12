using IdTheAthlete.Api.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

public class GameTokenServiceTests
{
    private const string Key = "test-signing-key-that-is-long-enough-123";
    private static readonly DailyGameState State = new(SportId: 3, PuzzleDate: new DateOnly(2026, 10, 12), GuessCount: 4);

    internal static GameTokenService Create(string? key = Key) =>
        new(Options.Create(new GameTokenOptions { SigningKey = key }), NullLogger<GameTokenService>.Instance);

    private static ValidateOptionsResult Validate(string? key, string environment)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environment);
        return new GameTokenOptionsValidator(env.Object).Validate(null, new GameTokenOptions { SigningKey = key });
    }

    [Fact]
    public void Read_TokenItIssued_ReturnsSameState()
    {
        var service = Create();

        var state = service.Read(service.Issue(State));

        Assert.Equal(State, state);
    }

    [Fact]
    public void Read_PayloadEditedToRaiseGuessCount_Throws()
    {
        var service = Create();
        var signature = service.Issue(State).Split('.')[1];
        var forgedPayload = service.Issue(State with { GuessCount = 7 }).Split('.')[0];

        Assert.Throws<InvalidOperationException>(() => service.Read($"{forgedPayload}.{signature}"));
    }

    [Fact]
    public void Read_TokenSignedWithDifferentKey_Throws()
    {
        var other = Create(key: "a-completely-different-signing-key-456");

        Assert.Throws<InvalidOperationException>(() => Create().Read(other.Issue(State)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("!!!.???")]
    [InlineData("a.b.c")]
    public void Read_Malformed_Throws(string token)
    {
        Assert.Throws<InvalidOperationException>(() => Create().Read(token));
    }

    [Fact]
    public void Create_NoKey_UsesWorkingRandomKey()
    {
        var service = Create(key: null);

        Assert.Equal(State, service.Read(service.Issue(State)));
    }

    [Fact]
    public void Validate_NoKeyOutsideDevelopment_Fails()
    {
        Assert.True(Validate(key: null, environment: "Production").Failed);
    }

    [Fact]
    public void Validate_NoKeyInDevelopment_Succeeds()
    {
        Assert.True(Validate(key: null, environment: "Development").Succeeded);
    }

    [Fact]
    public void Validate_KeyTooShort_Fails()
    {
        Assert.True(Validate(key: "short", environment: "Production").Failed);
    }

    [Fact]
    public void Validate_LongEnoughKey_Succeeds()
    {
        Assert.True(Validate(key: Key, environment: "Production").Succeeded);
    }
}
