using IdTheAthlete.Api.Services;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

public class SettingsFailureLogThrottleTests
{
    [Fact]
    public void Logs_each_key_at_most_once_per_window()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var throttle = new SettingsFailureLogThrottle(time);

        Assert.True(throttle.ShouldLog("A"));
        Assert.False(throttle.ShouldLog("A"));
        Assert.True(throttle.ShouldLog("B"));   // a different key is never held back by A

        time.Advance(SettingsFailureLogThrottle.Window - TimeSpan.FromSeconds(1));
        Assert.False(throttle.ShouldLog("A"));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(throttle.ShouldLog("A"));
        Assert.False(throttle.ShouldLog("A"));
    }
}
