using IdTheAthlete.Api.Services;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

public class SettingsFailureLogThrottleTests
{
    private static FakeTimeProvider NewClock() => new(new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Logs_each_key_at_most_once_per_window()
    {
        var time = NewClock();
        var throttle = new SettingsFailureLogThrottle(time);

        Assert.True(throttle.ShouldLog("A", out _));
        Assert.False(throttle.ShouldLog("A", out _));
        Assert.True(throttle.ShouldLog("B", out _));   // a different key is never held back by A

        time.Advance(SettingsFailureLogThrottle.Window - TimeSpan.FromSeconds(1));
        Assert.False(throttle.ShouldLog("A", out _));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(throttle.ShouldLog("A", out _));
        Assert.False(throttle.ShouldLog("A", out _));
    }

    [Fact]
    public void Reports_how_many_failures_were_suppressed_and_starts_again_from_zero()
    {
        var time = NewClock();
        var throttle = new SettingsFailureLogThrottle(time);

        Assert.True(throttle.ShouldLog("A", out var first));
        Assert.Equal(0, first);

        for (var i = 0; i < 3; i++)
            Assert.False(throttle.ShouldLog("A", out _));
        Assert.True(throttle.ShouldLog("B", out var b));   // B has its own count, untouched by A's
        Assert.Equal(0, b);

        time.Advance(SettingsFailureLogThrottle.Window);
        Assert.True(throttle.ShouldLog("A", out var afterThree));
        Assert.Equal(3, afterThree);

        Assert.False(throttle.ShouldLog("A", out _));
        time.Advance(SettingsFailureLogThrottle.Window);
        Assert.True(throttle.ShouldLog("A", out var afterOne));
        Assert.Equal(1, afterOne);

        time.Advance(SettingsFailureLogThrottle.Window);
        Assert.True(throttle.ShouldLog("A", out var afterNone));
        Assert.Equal(0, afterNone);
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(1, " (1 failure suppressed since last log)")]
    [InlineData(3, " (3 failures suppressed since last log)")]
    public void Suppressed_note_reads_naturally(int suppressed, string expected)
    {
        Assert.Equal(expected, SettingsFailureLogThrottle.SuppressedNote(suppressed));
    }
}
