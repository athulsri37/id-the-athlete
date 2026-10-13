using IdTheAthlete.Api.Middleware;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Middleware;

// 5 wrong admin keys within 15 minutes lock an address out until the oldest
// of them is 15 minutes old; a right key clears its failures.
public class AdminLoginThrottleTests
{
    private const string Client = "203.0.113.7";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly AdminLoginThrottle _throttle;

    public AdminLoginThrottleTests()
    {
        _throttle = new AdminLoginThrottle(_time);
    }

    private void Fail(int times, string client = Client)
    {
        for (var i = 0; i < times; i++)
            _throttle.RecordFailure(client);
    }

    [Fact]
    public void FourFailures_StillAllowed()
    {
        Fail(4);

        Assert.Null(_throttle.LockedOutFor(Client));
    }

    [Fact]
    public void FiveFailures_LockOutFor15Minutes()
    {
        Fail(5);

        Assert.Equal(TimeSpan.FromMinutes(15), _throttle.LockedOutFor(Client));
    }

    [Fact]
    public void TheLockout_EndsWhenTheOldestFailureIs15MinutesOld()
    {
        Fail(1);
        _time.Advance(TimeSpan.FromMinutes(10));
        Fail(4);

        Assert.Equal(TimeSpan.FromMinutes(5), _throttle.LockedOutFor(Client));
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Null(_throttle.LockedOutFor(Client));
    }

    [Fact]
    public void FailuresSpreadOverMoreThan15Minutes_NeverLockOut()
    {
        for (var i = 0; i < 10; i++)
        {
            Fail(1);
            _time.Advance(TimeSpan.FromMinutes(4));
        }

        Assert.Null(_throttle.LockedOutFor(Client));
    }

    [Fact]
    public void ARightKey_ClearsTheFailures()
    {
        Fail(4);
        _throttle.RecordSuccess(Client);
        Fail(4);

        Assert.Null(_throttle.LockedOutFor(Client));
    }

    [Fact]
    public void EachAddress_IsCountedSeparately()
    {
        Fail(5);

        Assert.Null(_throttle.LockedOutFor("198.51.100.9"));
    }

    [Fact]
    public void AddressesWhoseFailuresExpired_AreForgotten()
    {
        Fail(1, "198.51.100.1");
        Fail(1, "198.51.100.2");
        _time.Advance(TimeSpan.FromMinutes(16));

        Fail(1); // the next failure sweeps the expired ones out

        Assert.Equal(1, _throttle.TrackedClients);
    }
}
