using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace IdTheAthlete.Api.Tests.Postgres;

// What the real database guarantees, checked against Postgres itself: the
// unique indexes the app relies on, the daily-puzzle race being handled,
// and the stats table's foreign key and stored values.
[Collection(PostgresCollection.Name)]
public class PostgresBehaviourTests
{
    private const string UniqueViolation = "23505";

    private readonly PostgresFixture _postgres;

    public PostgresBehaviourTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    // The reference-data migration creates the sports; tests look them up.
    private static int SportId(GameDbContext db, string slug) => db.Sports.Single(s => s.Slug == slug).Id;

    private static string Unique(string name) => $"{name} {Guid.NewGuid():N}";

    private static async Task<List<Player>> AddPlayersAsync(GameDbContext db, int sportId, int count)
    {
        var players = Enumerable.Range(0, count)
            .Select(_ => new Player { SportId = sportId, Name = Unique("Player") })
            .ToList();
        db.Players.AddRange(players);
        await db.SaveChangesAsync();
        return players;
    }

    private static void AssertUniqueViolation(DbUpdateException ex) =>
        Assert.Equal(UniqueViolation, Assert.IsType<PostgresException>(ex.InnerException).SqlState);

    [Fact]
    public async Task PlayerNames_AreUniqueWithinASport_NotAcrossSports()
    {
        await using var db = _postgres.CreateContext();
        var name = Unique("Same Name");
        var men = SportId(db, "tennis-men");
        db.Players.Add(new Player { SportId = men, Name = name });
        db.Players.Add(new Player { SportId = SportId(db, "tennis-women"), Name = name });
        await db.SaveChangesAsync();

        await using var again = _postgres.CreateContext();
        again.Players.Add(new Player { SportId = men, Name = name });

        AssertUniqueViolation(await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync()));
    }

    [Fact]
    public async Task EveryTour_HasItsWebAddress_UniqueWithinItsFamily()
    {
        await using var db = _postgres.CreateContext();
        var addresses = await db.Sports.OrderBy(s => s.Slug).Select(s => s.Family + "/" + s.UrlName).ToListAsync();
        Assert.Equal(new[] { "cricket/men", "cricket/women", "tennis/men", "tennis/women" }, addresses);

        db.Sports.Add(new Sport { Name = "Clash", Slug = Unique("tennis-clash"), Family = "tennis", UrlName = "men" });
        AssertUniqueViolation(await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()));
    }

    [Fact]
    public async Task DailyPuzzles_AreOnePerSportPerDate()
    {
        await using var db = _postgres.CreateContext();
        var sport = SportId(db, "cricket-men-international");
        var players = await AddPlayersAsync(db, sport, 2);
        var date = new DateOnly(1990, 1, 1);
        db.DailyPuzzles.Add(new DailyPuzzle { SportId = sport, PlayerId = players[0].Id, PuzzleDate = date });
        await db.SaveChangesAsync();

        await using var again = _postgres.CreateContext();
        again.DailyPuzzles.Add(new DailyPuzzle { SportId = sport, PlayerId = players[1].Id, PuzzleDate = date });

        AssertUniqueViolation(await Assert.ThrowsAsync<DbUpdateException>(() => again.SaveChangesAsync()));
    }

    [Fact]
    public async Task EnsuringTheSameDailyPuzzleAtOnce_CreatesExactlyOne_AndNeverThrows()
    {
        int sport;
        await using (var setup = _postgres.CreateContext())
        {
            sport = SportId(setup, "tennis-women");
            await AddPlayersAsync(setup, sport, 5);
        }
        var date = new DateOnly(1991, 6, 1);

        // Like the scheduled job and a lazy fallback starting together, each
        // with its own request scope: whoever loses the insert race must
        // report "already there", not fail.
        var contexts = Enumerable.Range(0, 8).Select(_ => _postgres.CreateContext()).ToList();
        try
        {
            var created = await Task.WhenAll(contexts.Select(db =>
                new DailyPuzzleService(db, NullLogger<DailyPuzzleService>.Instance, TimeProvider.System)
                    .EnsureDailyPuzzleAsync(sport, date)));

            Assert.Equal(1, created.Count(c => c));
        }
        finally
        {
            foreach (var db in contexts)
                await db.DisposeAsync();
        }

        await using var check = _postgres.CreateContext();
        Assert.Equal(1, await check.DailyPuzzles.CountAsync(d => d.SportId == sport && d.PuzzleDate == date));
    }

    [Fact]
    public async Task DeletingAnAthlete_KeepsTheStatsOfGamesPlayedOnThem()
    {
        await using var db = _postgres.CreateContext();
        var sport = SportId(db, "cricket-women-international");
        var athlete = (await AddPlayersAsync(db, sport, 1))[0];
        var gameId = Guid.NewGuid();
        await new GameStatsService(db, TimeProvider.System, NullLogger<GameStatsService>.Instance)
            .RecordGuessAsync(new GameGuessStat(gameId, sport, "easy", null, athlete.Id, 1, GameOver: false, Won: false));

        db.Players.Remove(athlete);
        await db.SaveChangesAsync();

        await using var check = _postgres.CreateContext();
        var record = await check.GameRecords.SingleAsync(r => r.Id == gameId);
        Assert.Null(record.MysteryPlayerId);
    }

    [Fact]
    public async Task GameStats_StoreTheResultAsText_AndSkipUnrecordedGamesQuietly()
    {
        await using var db = _postgres.CreateContext();
        var sport = SportId(db, "tennis-men");
        var athlete = (await AddPlayersAsync(db, sport, 1))[0];
        var logger = new CapturingLogger<GameStatsService>();
        var stats = new GameStatsService(db, TimeProvider.System, logger);
        var gameId = Guid.NewGuid();

        await stats.RecordGuessAsync(new GameGuessStat(gameId, sport, "daily", new DateOnly(1992, 3, 4), athlete.Id, 1, false, false));
        await stats.RecordHintAsync(gameId, 5);
        await stats.RecordGuessAsync(new GameGuessStat(gameId, sport, "daily", new DateOnly(1992, 3, 4), athlete.Id, 6, GameOver: true, Won: true));
        // A later guess of a game whose first guess was never recorded.
        await stats.RecordGuessAsync(new GameGuessStat(Guid.NewGuid(), sport, "daily", null, athlete.Id, 4, false, false));

        // The README's queries compare the Result column as text.
        await using var conn = new NpgsqlConnection(db.Database.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """SELECT "Result", "GuessCount", "HintTakenAtGuess" FROM "GameRecords" WHERE "Id" = @id""", conn);
        cmd.Parameters.AddWithValue("id", gameId);
        await using var row = await cmd.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal(("Won", 6, 5), (row.GetString(0), row.GetInt32(1), row.GetInt32(2)));

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
    }
}
