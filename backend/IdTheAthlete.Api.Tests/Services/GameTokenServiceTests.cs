using IdTheAthlete.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IdTheAthlete.Api.Tests.Services;

public class GameTokenServiceTests
{
    private const string Key = "test-signing-key-that-is-long-enough-123";
    private static readonly DailyGameState State = new(SportId: 3, PuzzleDate: new DateOnly(2026, 10, 12), GuessCount: 4);

    internal static GameTokenService Create(string? key = Key, string environment = "Production")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [GameTokenService.SigningKeyConfigKey] = key })
            .Build();
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environment);

        return new GameTokenService(config, env.Object, NullLogger<GameTokenService>.Instance);
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
    public void Constructor_NoKeyOutsideDevelopment_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Create(key: null, environment: "Production"));
    }

    [Fact]
    public void Constructor_NoKeyInDevelopment_UsesRandomKey()
    {
        var service = Create(key: null, environment: "Development");

        Assert.Equal(State, service.Read(service.Issue(State)));
    }

    [Fact]
    public void Constructor_KeyTooShort_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Create(key: "short"));
    }
}
