using IdTheAthlete.Api.Data;

namespace IdTheAthlete.Api.Health;

// GET /api/health/ready: answers "ok" only when the API can also reach its
// database, for an uptime monitor. /api/health stays a plain "the process
// is up" check, because it's Render's Health Check Path: tying it to the
// database would have Render restart the API, or refuse a deploy, over a
// database problem a restart can't fix.
public sealed class ReadinessCheck
{
    private ReadinessCheck()
    {
    }

    public static async Task<IResult> CheckAsync(GameDbContext db, ILogger<ReadinessCheck> logger, CancellationToken cancellationToken)
    {
        // CanConnectAsync answers false (rather than throwing) when the
        // database is down, unreachable or refusing the API's login.
        if (await db.Database.CanConnectAsync(cancellationToken))
            return Results.Ok(new { status = "ok" });

        logger.LogWarning("Readiness check failed: the API can't reach its database.");
        return Results.Json(new { status = "database unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
