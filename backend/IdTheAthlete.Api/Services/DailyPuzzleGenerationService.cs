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
    private readonly TimeProvider _timeProvider;

    public DailyPuzzleGenerationService(IServiceScopeFactory scopeFactory, ILogger<DailyPuzzleGenerationService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
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
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var delay = TimeUntilNextMidnightUtc(now);
            _logger.LogInformation(
                "Next scheduled daily puzzle generation in {Delay} (at {NextRunUtc:u}).",
                delay, now.Add(delay));

            try
            {
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await GenerateForAllSportsAsync(stoppingToken);
        }
    }

    internal static TimeSpan TimeUntilNextMidnightUtc(DateTime utcNow)
    {
        var nextMidnight = utcNow.Date.AddDays(1);
        return nextMidnight - utcNow;
    }

    private async Task GenerateForAllSportsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var dailyPuzzleService = scope.ServiceProvider.GetRequiredService<IDailyPuzzleService>();

        var sports = await db.Sports.ToListAsync(stoppingToken);
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

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
