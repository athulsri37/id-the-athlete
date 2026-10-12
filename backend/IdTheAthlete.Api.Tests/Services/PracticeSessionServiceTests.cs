using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

// A practice game stays playable while it's being played, and is dropped
// once it goes the idle timeout without a guess or hint.
public class PracticeSessionServiceTests
{
    private const int PlayerId = 7;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(90);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero));
    private readonly PracticeSessionService _sessions;

    public PracticeSessionServiceTests()
    {
        _sessions = Create(_time);
    }

    internal static PracticeSessionService Create(TimeProvider? time = null) =>
        new(Options.Create(new PracticeSessionOptions { IdleTimeout = IdleTimeout }), time ?? TimeProvider.System);

    [Fact]
    public void NewSession_ResolvesItsPlayerWithNoGuesses()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        Assert.Equal(PlayerId, _sessions.ResolveSessionPlayerId(sessionId));
        Assert.Equal(0, _sessions.GetGuessCount(sessionId));
    }

    [Fact]
    public void RecordGuess_CountsEachGuess()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        _sessions.RecordGuess(sessionId);

        Assert.Equal(2, _sessions.RecordGuess(sessionId));
    }

    [Fact]
    public void IdleForExactlyTheTimeout_StillPlayable()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        _time.Advance(IdleTimeout);

        Assert.Equal(1, _sessions.RecordGuess(sessionId));
    }

    [Fact]
    public void IdleLongerThanTheTimeout_IsRefusedAndDropped()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        _time.Advance(IdleTimeout + TimeSpan.FromSeconds(1));

        Assert.Throws<InvalidRequestException>(() => _sessions.RecordGuess(sessionId));
        Assert.Equal(0, _sessions.StoredSessionCount);
    }

    [Fact]
    public void EachGuess_RestartsTheIdleClock()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        for (var i = 1; i <= 3; i++)
        {
            _time.Advance(IdleTimeout - TimeSpan.FromMinutes(1));
            Assert.Equal(i, _sessions.RecordGuess(sessionId));
        }
    }

    [Fact]
    public void HintRead_AlsoRestartsTheIdleClock()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        // The hint endpoint reads the player and guess count without guessing.
        _time.Advance(IdleTimeout - TimeSpan.FromMinutes(1));
        _sessions.GetGuessCount(sessionId);
        _time.Advance(IdleTimeout - TimeSpan.FromMinutes(1));

        Assert.Equal(PlayerId, _sessions.ResolveSessionPlayerId(sessionId));
    }

    [Fact]
    public void RemovedSession_IsRefused()
    {
        var sessionId = _sessions.CreateSession(PlayerId);

        _sessions.RemoveSession(sessionId);

        Assert.Throws<InvalidRequestException>(() => _sessions.ResolveSessionPlayerId(sessionId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-such-session")]
    public void UnknownSession_IsRefused(string? sessionId)
    {
        Assert.Throws<InvalidRequestException>(() => _sessions.GetGuessCount(sessionId));
    }

    [Fact]
    public void CreatingASession_SweepsOutAbandonedOnes()
    {
        // Never touched again: the abandoned games the sweep exists for.
        _sessions.CreateSession(PlayerId);
        _sessions.CreateSession(PlayerId);
        var active = _sessions.CreateSession(PlayerId);

        _time.Advance(IdleTimeout - TimeSpan.FromMinutes(1));
        _sessions.RecordGuess(active);
        _time.Advance(TimeSpan.FromMinutes(2));

        var fresh = _sessions.CreateSession(PlayerId);

        Assert.Equal(2, _sessions.StoredSessionCount);
        Assert.Equal(1, _sessions.GetGuessCount(active));
        Assert.Equal(0, _sessions.GetGuessCount(fresh));
    }

    [Fact]
    public void Sweep_RunsAtMostOncePerInterval()
    {
        _sessions.CreateSession(PlayerId);                  // 12:00, idle from 13:30
        _time.Advance(TimeSpan.FromMinutes(89));
        _sessions.CreateSession(PlayerId);                  // 13:29 sweeps; nothing idle yet
        _time.Advance(TimeSpan.FromMinutes(2));

        _sessions.CreateSession(PlayerId);                  // 13:31: first one idle, but next sweep is 13:34
        Assert.Equal(3, _sessions.StoredSessionCount);

        _time.Advance(TimeSpan.FromMinutes(3));
        _sessions.CreateSession(PlayerId);                  // 13:34 sweeps it out
        Assert.Equal(3, _sessions.StoredSessionCount);
    }
}
