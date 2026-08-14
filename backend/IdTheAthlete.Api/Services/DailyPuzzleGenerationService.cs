using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;

namespace IdTheAthlete.Api.Services;

// Generates every Sport's Daily Challenge puzzle on a fixed 00:00 UTC
// schedule, replacing pure lazy/on-demand generation as the primary
// mechanism (DailyPuzzleService keeps a lazy fallback for the rare case
// this service and its startup catch-up both somehow miss a sport for a
// day -- see its warning log if that happens).
//
// Loops over every row in the Sports table rather than any hardcoded list,
// so a future sport (e.g. Cricket Domestic) is covered automatically with
// no code change here.
public class DailyPuzzleGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyPuzzleGenerationService> _logger;

    public DailyPuzzleGenerationService(IServiceScopeFactory scopeFactory, ILogger<DailyPuzzleGenerationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup catch-up: covers the case the app was offline at the
        // scheduled 00:00 UTC run (or is starting for the very first time)
        // by generating any sport's puzzle for today that's still missing,
        // immediately, before waiting for the next scheduled cycle.
        _logger.LogInformation("Running daily puzzle startup catch-up check.");
        await GenerateForAllSportsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextMidnightUtc();
            _logger.LogInformation(
                "Next scheduled daily puzzle generation in {Delay} (at {NextRunUtc:u}).",
                delay, DateTime.UtcNow.Add(delay));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await GenerateForAllSportsAsync(stoppingToken);
        }
    }

    // Exposed as internal + static, with an injectable "now", so it can be
    // verified directly (e.g. from a test or a scratch console) without
    // waiting for real-world midnight.
    internal static TimeSpan TimeUntilNextMidnightUtc(DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        return nextMidnight - now;
    }

    private async Task GenerateForAllSportsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var dailyPuzzleService = scope.ServiceProvider.GetRequiredService<DailyPuzzleService>();

        var sports = await db.Sports.ToListAsync(stoppingToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var sport in sports)
        {
            try
            {
                var created = await dailyPuzzleService.EnsureDailyPuzzleAsync(sport.Id, today);
                if (created)
                {
                    _logger.LogInformation(
                        "Generated daily puzzle for sport '{SportSlug}' ({SportId}) for {Date}.",
                        sport.Slug, sport.Id, today);
                }
                else
                {
                    _logger.LogDebug(
                        "Daily puzzle for sport '{SportSlug}' ({SportId}) for {Date} already existed; skipped.",
                        sport.Slug, sport.Id, today);
                }
            }
            catch (Exception ex)
            {
                // One sport's failure (e.g. an empty player pool) shouldn't
                // block generation for the rest.
                _logger.LogError(ex,
                    "Failed to generate daily puzzle for sport '{SportSlug}' ({SportId}) for {Date}.",
                    sport.Slug, sport.Id, today);
            }
        }
    }
}
