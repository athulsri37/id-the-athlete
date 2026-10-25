using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Health;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IdTheAthlete.Api.Tests.Postgres;

// The uptime monitor's check: ok only when the database answers, and a
// logged 503 (not an exception) when it doesn't.
[Collection(PostgresCollection.Name)]
public class ReadinessCheckTests
{
    private readonly PostgresFixture _postgres;

    public ReadinessCheckTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task WithTheDatabaseUp_IsOk()
    {
        await using var db = _postgres.CreateContext();
        var logger = new CapturingLogger<ReadinessCheck>();

        var result = await ReadinessCheck.CheckAsync(db, logger, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task WithTheDatabaseUnreachable_Is503AndLogsAWarning()
    {
        // Nothing listens on port 1, so the connection is refused at once.
        await using var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=3")
            .Options);
        var logger = new CapturingLogger<ReadinessCheck>();

        var result = await ReadinessCheck.CheckAsync(db, logger, CancellationToken.None);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("can't reach its database", entry.Message);
    }
}
