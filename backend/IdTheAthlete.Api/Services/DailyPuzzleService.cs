using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// Daily-puzzle selection, lookup, and persistence -- the "which player is
// today's/this date's mystery player" concern. Complementary to (and used
// by) DailyPuzzleGenerationService, which only owns the midnight-UTC
// scheduling; the actual puzzle-selection logic lives here so it has a
// single home regardless of which caller triggers it (the scheduled job,
// its startup catch-up, or the lazy fallback below). Registered Scoped
// (depends on GameDbContext).
public class DailyPuzzleService
{
    private readonly GameDbContext _db;
    private readonly ILogger<DailyPuzzleService> _logger;
    private static readonly Random Rng = new();

    public DailyPuzzleService(GameDbContext db, ILogger<DailyPuzzleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Every date (yyyy-MM-dd) that has an existing Daily Challenge puzzle
    // for this sport, most recent first. Deliberately just the dates --
    // completion/streak status is tracked entirely client-side (localStorage),
    // never here.
    public async Task<List<string>> GetDailyPuzzleDatesAsync(string sportSlug)
    {
        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Slug == sportSlug)
            ?? throw new InvalidOperationException($"Sport '{sportSlug}' not found");

        var dates = await _db.DailyPuzzles
            .Where(d => d.SportId == sport.Id)
            .OrderByDescending(d => d.PuzzleDate)
            .Select(d => d.PuzzleDate)
            .ToListAsync();

        return dates.Select(d => d.ToString("yyyy-MM-dd")).ToList();
    }

    // date == null means "today" (the existing behavior: creates today's
    // puzzle on first access if it doesn't exist yet). A specific date is
    // always a Past Challenge, which by definition already has a puzzle
    // (the frontend only ever offers dates GetDailyPuzzleDatesAsync
    // returned) -- so this never creates one, only looks it up.
    public async Task<int> ResolveDailyMysteryPlayerIdAsync(int sportId, string? date)
    {
        if (date == null)
            return await GetTodaysMysteryPlayerIdAsync(sportId);

        if (!DateOnly.TryParse(date, out var parsedDate))
            throw new InvalidOperationException($"Invalid date '{date}'.");

        var puzzle = await _db.DailyPuzzles
            .FirstOrDefaultAsync(d => d.SportId == sportId && d.PuzzleDate == parsedDate)
            ?? throw new InvalidOperationException($"No daily puzzle exists for {date}.");

        return puzzle.PlayerId;
    }

    // Lazy/on-demand fallback: only reached when a player's request for
    // today's Daily Challenge arrives before either the scheduled midnight-
    // UTC job (DailyPuzzleGenerationService) or its startup catch-up have
    // generated today's puzzle for this sport. This used to be the ONLY
    // way daily puzzles were generated; it's now purely a safety net, so
    // it logs a warning when it actually creates a puzzle -- that should
    // never happen in normal operation, and means the scheduled mechanism
    // needs investigating.
    private async Task<int> GetTodaysMysteryPlayerIdAsync(int sportId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _db.DailyPuzzles
            .FirstOrDefaultAsync(d => d.SportId == sportId && d.PuzzleDate == today);

        if (existing != null)
            return existing.PlayerId;

        _logger.LogWarning(
            "Lazy-generating today's ({Date}) daily puzzle for sport {SportId} -- neither the scheduled " +
            "midnight-UTC job nor its startup catch-up had created one yet. This is only a safety net; " +
            "if it fires in normal operation, DailyPuzzleGenerationService needs investigating.",
            today, sportId);

        var chosen = await SelectDailyPuzzlePlayerAsync(sportId, today);

        _db.DailyPuzzles.Add(new DailyPuzzle
        {
            SportId = sportId,
            PlayerId = chosen.Id,
            PuzzleDate = today
        });
        await _db.SaveChangesAsync();

        return chosen.Id;
    }

    // Shared by both the lazy fallback above and EnsureDailyPuzzleAsync
    // below, so the "avoid repeats from the last 14 days" rule is defined
    // in exactly one place regardless of which path generates a puzzle.
    private async Task<Player> SelectDailyPuzzlePlayerAsync(int sportId, DateOnly date)
    {
        var cutoff = date.AddDays(-14);
        var recentPlayerIds = await _db.DailyPuzzles
            .Where(d => d.SportId == sportId && d.PuzzleDate > cutoff)
            .Select(d => d.PlayerId)
            .ToListAsync();

        var eligiblePlayers = await _db.Players
            .Where(p => p.SportId == sportId && !recentPlayerIds.Contains(p.Id))
            .ToListAsync();

        // Fall back to the full pool if everything has been used recently
        // (only realistic once the dataset is still small).
        if (eligiblePlayers.Count == 0)
        {
            eligiblePlayers = await _db.Players.Where(p => p.SportId == sportId).ToListAsync();
        }

        return eligiblePlayers[Rng.Next(eligiblePlayers.Count)];
    }

    // Called by DailyPuzzleGenerationService (both its startup catch-up and
    // its scheduled midnight-UTC run) -- the primary, expected path for
    // creating daily puzzles. Idempotent: returns false without touching
    // the database if a puzzle for this sport/date already exists. Also
    // guards against a duplicate-insert race (e.g. the scheduled job and
    // the lazy fallback both reaching the "doesn't exist yet" check at
    // nearly the same moment) by treating the DailyPuzzles unique index
    // violation on (SportId, PuzzleDate) as "someone else just created it"
    // rather than letting the exception propagate.
    public async Task<bool> EnsureDailyPuzzleAsync(int sportId, DateOnly date)
    {
        var alreadyExists = await _db.DailyPuzzles.AnyAsync(d => d.SportId == sportId && d.PuzzleDate == date);
        if (alreadyExists)
            return false;

        var chosen = await SelectDailyPuzzlePlayerAsync(sportId, date);

        _db.DailyPuzzles.Add(new DailyPuzzle
        {
            SportId = sportId,
            PlayerId = chosen.Id,
            PuzzleDate = date
        });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race to another concurrent generator. If a puzzle now
            // exists for this sport/date (just not the one we picked),
            // that's exactly the outcome we wanted, so treat it as such
            // rather than letting the exception propagate. Any other cause
            // of the same exception type is rare enough here (SQLite/PG
            // unique-violation is by far the likely one) that re-throwing
            // would be the only alternative anyway.
            var nowExists = await _db.DailyPuzzles.AnyAsync(d => d.SportId == sportId && d.PuzzleDate == date);
            if (!nowExists)
                throw;

            return false;
        }

        return true;
    }
}
