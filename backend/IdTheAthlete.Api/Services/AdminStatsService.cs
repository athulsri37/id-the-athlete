using System.Globalization;
using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Services;

// The admin Stats tab: the README's four "Game Stats" views, computed in
// the database over the games started in a date range (UTC, both ends
// inclusive), for one tour or all of them. Registered Scoped (depends on
// GameDbContext).
public sealed class AdminStatsService : IAdminStatsService
{
    // An unfinished game counts as abandoned once this long passes without
    // a guess; the README's queries use the same 90 minutes.
    public static readonly TimeSpan AbandonedAfter = TimeSpan.FromMinutes(90);

    private const string PastChallengeMode = "past challenge";
    private static readonly string[] ModeOrder = { "daily", PastChallengeMode, "easy", "medium", "hard" };

    private readonly GameDbContext _db;
    private readonly TimeProvider _timeProvider;

    public AdminStatsService(GameDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;
    }

    public async Task<AdminStatsDto> GetStatsAsync(string? sportSlug, string? from, string? to)
    {
        var fromDate = ParseDate(from, nameof(from));
        var toDate = ParseDate(to, nameof(to));
        if (fromDate > toDate)
            throw new InvalidRequestException("'from' must not be after 'to'.");

        var tours = await _db.Sports.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Name);
        var games = _db.GameRecords.AsNoTracking();

        if (!string.IsNullOrEmpty(sportSlug))
        {
            var sportId = await _db.Sports.Where(s => s.Slug == sportSlug).Select(s => (int?)s.Id).FirstOrDefaultAsync()
                ?? throw new NotFoundException($"Sport '{sportSlug}' not found");
            games = games.Where(g => g.SportId == sportId);
        }
        if (fromDate is { } start)
        {
            var startAt = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            games = games.Where(g => g.StartedAt >= startAt);
        }
        if (toDate is { } end)
        {
            var endBefore = end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            games = games.Where(g => g.StartedAt < endBefore);
        }

        return new AdminStatsDto
        {
            Outcomes = await GetOutcomesAsync(games, tours),
            WinningGuesses = await GetWinningGuessesAsync(games, tours),
            Hints = await GetHintsAsync(games, tours),
            Puzzles = await GetPuzzlesAsync(games, tours),
        };
    }

    private async Task<List<AdminStatsOutcomeRow>> GetOutcomesAsync(IQueryable<GameRecord> games, Dictionary<int, string> tours)
    {
        var abandonedBefore = _timeProvider.GetUtcNow().UtcDateTime - AbandonedAfter;
        var groups = await games
            .GroupBy(g => new { g.SportId, Mode = g.IsPastChallenge ? PastChallengeMode : g.Mode })
            .Select(x => new
            {
                x.Key.SportId,
                x.Key.Mode,
                Games = x.Count(),
                Won = x.Count(g => g.Result == GameResult.Won),
                Lost = x.Count(g => g.Result == GameResult.Lost),
                Abandoned = x.Count(g => g.Result == null && g.LastActivityAt < abandonedBefore),
            })
            .ToListAsync();

        return groups
            .Select(x => new AdminStatsOutcomeRow
            {
                Tour = tours[x.SportId],
                Mode = x.Mode,
                Games = x.Games,
                Won = x.Won,
                Lost = x.Lost,
                Abandoned = x.Abandoned,
                InProgress = x.Games - x.Won - x.Lost - x.Abandoned,
                FinishedPercent = Percent(x.Won + x.Lost, x.Games) ?? 0,
            })
            .OrderBy(r => r.Tour)
            .ThenBy(r => Array.IndexOf(ModeOrder, r.Mode))
            .ToList();
    }

    private static async Task<List<AdminStatsWinningGuessRow>> GetWinningGuessesAsync(IQueryable<GameRecord> games, Dictionary<int, string> tours)
    {
        var groups = await games
            .Where(g => g.Result == GameResult.Won)
            .GroupBy(g => new { g.SportId, g.GuessCount })
            .Select(x => new { x.Key.SportId, x.Key.GuessCount, Games = x.Count() })
            .ToListAsync();

        return groups
            .Select(x => new AdminStatsWinningGuessRow { Tour = tours[x.SportId], Guess = x.GuessCount, Games = x.Games })
            .OrderBy(r => r.Tour)
            .ThenBy(r => r.Guess)
            .ToList();
    }

    // Only finished games that got as far as the hint, so "with" and
    // "without" compare players who were both offered it.
    private static async Task<List<AdminStatsHintRow>> GetHintsAsync(IQueryable<GameRecord> games, Dictionary<int, string> tours)
    {
        var groups = await games
            .Where(g => g.GuessCount >= GameRules.HintAfterGuess && g.Result != null)
            .GroupBy(g => g.SportId)
            .Select(x => new
            {
                SportId = x.Key,
                Reached = x.Count(),
                Took = x.Count(g => g.HintTakenAtGuess != null),
                WonWithHint = x.Count(g => g.HintTakenAtGuess != null && g.Result == GameResult.Won),
                WonWithoutHint = x.Count(g => g.HintTakenAtGuess == null && g.Result == GameResult.Won),
            })
            .ToListAsync();

        return groups
            .Select(x => new AdminStatsHintRow
            {
                Tour = tours[x.SportId],
                ReachedHint = x.Reached,
                TookHint = x.Took,
                WinPercentWithHint = Percent(x.WonWithHint, x.Took),
                WinPercentWithoutHint = Percent(x.WonWithoutHint, x.Reached - x.Took),
            })
            .OrderBy(r => r.Tour)
            .ToList();
    }

    private async Task<List<AdminStatsPuzzleRow>> GetPuzzlesAsync(IQueryable<GameRecord> games, Dictionary<int, string> tours)
    {
        var groups = await games
            .Where(g => g.Mode == "daily" && g.PuzzleDate != null)
            .GroupBy(g => new { g.SportId, g.PuzzleDate, g.MysteryPlayerId })
            .Select(x => new
            {
                x.Key.SportId,
                x.Key.PuzzleDate,
                x.Key.MysteryPlayerId,
                Games = x.Count(),
                Won = x.Count(g => g.Result == GameResult.Won),
                WinningGuesses = x.Sum(g => g.Result == GameResult.Won ? g.GuessCount : 0),
            })
            .ToListAsync();

        var playerIds = groups.Where(x => x.MysteryPlayerId != null).Select(x => x.MysteryPlayerId!.Value).Distinct().ToList();
        var athletes = await _db.Players.AsNoTracking()
            .Where(p => playerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        return groups
            .Select(x => new AdminStatsPuzzleRow
            {
                PuzzleDate = x.PuzzleDate!.Value,
                Tour = tours[x.SportId],
                // The athlete was deleted after the games were played.
                Athlete = x.MysteryPlayerId is { } id && athletes.TryGetValue(id, out var name) ? name : "(deleted)",
                Games = x.Games,
                WinPercent = Percent(x.Won, x.Games) ?? 0,
                AverageWinningGuess = x.Won == 0 ? null : Math.Round((decimal)x.WinningGuesses / x.Won, 1),
            })
            .OrderByDescending(r => r.PuzzleDate)
            .ThenBy(r => r.Tour)
            .ToList();
    }

    private static decimal? Percent(int part, int whole) =>
        whole == 0 ? null : Math.Round(100m * part / whole, 1);

    private static DateOnly? ParseDate(string? value, string name)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidRequestException($"Invalid '{name}' date '{value}'. Use yyyy-MM-dd.");
        return date;
    }
}
