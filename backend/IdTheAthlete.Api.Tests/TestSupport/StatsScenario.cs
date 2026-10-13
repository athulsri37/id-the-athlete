using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Tests.TestSupport;

// One set of games covering every case the admin Stats tab reports, with
// the answers worked out by hand. Used against the in-memory database and
// against real Postgres, so the grouped queries are known to translate.
// All games start on Today (UTC) except one a month earlier; "now" is
// noon on Today.
internal static class StatsScenario
{
    public const string MenTour = "Men's Tennis";
    public const string WomenTour = "Women's Tennis";

    public static DateTimeOffset Noon(DateOnly today) => new(today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);

    public static void Seed(GameDbContext db, DateOnly today, int menSportId, int womenSportId, int menAthleteId, int womenAthleteId)
    {
        DateTime At(int hour, int daysAgo = 0) => today.AddDays(-daysAgo).ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc);

        GameRecord Game(int sportId, int athleteId, string mode, int guesses, GameResult? result,
            DateTime started, DateTime lastActivity, int? hintAt = null, DateOnly? puzzleDate = null, bool past = false) => new()
        {
            Id = Guid.NewGuid(),
            SportId = sportId,
            Mode = mode,
            PuzzleDate = mode == "daily" ? puzzleDate ?? today : null,
            IsPastChallenge = past,
            MysteryPlayerId = athleteId,
            GuessCount = guesses,
            HintTakenAtGuess = hintAt,
            Result = result,
            StartedAt = started,
            LastActivityAt = lastActivity,
            EndedAt = result == null ? null : lastActivity,
        };

        db.GameRecords.AddRange(
            // Men's daily: won on 3; won on 5 with the hint; lost with the
            // hint; still being played (last guess an hour ago); abandoned
            // (last guess three hours ago).
            Game(menSportId, menAthleteId, "daily", 3, GameResult.Won, At(8), At(8)),
            Game(menSportId, menAthleteId, "daily", 5, GameResult.Won, At(8), At(9), hintAt: 5),
            Game(menSportId, menAthleteId, "daily", 8, GameResult.Lost, At(9), At(9), hintAt: 5),
            Game(menSportId, menAthleteId, "daily", 2, null, At(10), At(11)),
            Game(menSportId, menAthleteId, "daily", 4, null, At(8), At(9)),
            // A Past Challenge won on 2, and an Easy practice game lost.
            Game(menSportId, menAthleteId, "daily", 2, GameResult.Won, At(10), At(10), puzzleDate: today.AddDays(-3), past: true),
            Game(menSportId, menAthleteId, "easy", 8, GameResult.Lost, At(7), At(7)),
            // Women's Hard practice game won on 6 without the hint.
            Game(womenSportId, womenAthleteId, "hard", 6, GameResult.Won, At(10), At(10)),
            // A month earlier: outside a range that starts this month.
            Game(menSportId, menAthleteId, "daily", 1, GameResult.Won, At(10, daysAgo: 30), At(10, daysAgo: 30), puzzleDate: today.AddDays(-30)));
        db.SaveChanges();
    }

    // The expected stats for games started from the first of Today's month
    // to Today, for both tours.
    public static void AssertThisMonth(AdminStatsDto stats, DateOnly today, string menAthlete)
    {
        Assert.Equal(new[]
        {
            (MenTour, "daily", 5, 2, 1, 1, 1, 60.0m),
            (MenTour, "past challenge", 1, 1, 0, 0, 0, 100m),
            (MenTour, "easy", 1, 0, 1, 0, 0, 100m),
            (WomenTour, "hard", 1, 1, 0, 0, 0, 100m),
        }, stats.Outcomes.Select(r => (r.Tour, r.Mode, r.Games, r.Won, r.Lost, r.Abandoned, r.InProgress, r.FinishedPercent)));

        Assert.Equal(new[] { (MenTour, 2, 1), (MenTour, 3, 1), (MenTour, 5, 1), (WomenTour, 6, 1) },
            stats.WinningGuesses.Select(r => (r.Tour, r.Guess, r.Games)));

        Assert.Equal(new[]
        {
            (MenTour, 3, 2, (decimal?)50.0m, (decimal?)0m),
            (WomenTour, 1, 0, (decimal?)null, (decimal?)100m),
        }, stats.Hints.Select(r => (r.Tour, r.ReachedHint, r.TookHint, r.WinPercentWithHint, r.WinPercentWithoutHint)));

        Assert.Equal(new[]
        {
            (today, MenTour, menAthlete, 5, 40.0m, (decimal?)4.0m),
            (today.AddDays(-3), MenTour, menAthlete, 1, 100m, (decimal?)2.0m),
        }, stats.Puzzles.Select(r => (r.PuzzleDate, r.Tour, r.Athlete, r.Games, r.WinPercent, r.AverageWinningGuess)));
    }

    public static string FirstOfMonth(DateOnly today) => new DateOnly(today.Year, today.Month, 1).ToString("yyyy-MM-dd");
}
