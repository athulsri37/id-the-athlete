using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Postgres;

// A rename's duplicate-name check, run by Postgres itself: the in-memory
// tests can't show how the database lowercases names, and a clash must be
// refused before it reaches the (SportId, Name) index, accented capitals
// included.
[Collection(PostgresCollection.Name)]
public class AdminPlayerRenamePostgresTests
{
    private readonly PostgresFixture _postgres;

    public AdminPlayerRenamePostgresTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    private static AdminService Admin(GameDbContext db)
    {
        var time = new FakeTimeProvider();
        var settings = new SettingsService(db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));
        return new AdminService(db, time, settings, new DifficultyService(TestGameServiceFactory.CreateRegistry(settings)),
            new AdminActor { Name = "Athul" });
    }

    [Theory]
    [InlineData("Ćorić {0}", "Ćorić {0}")]
    [InlineData("Ćorić {0}", "ćorić {0}")]
    [InlineData("Borna Coric {0}", "BORNA CORIC {0}")]
    public async Task ANameAnotherPlayerInTheTourHas_IsRefused_NotFailedOnTheIndex(string taken, string wanted)
    {
        var id = Guid.NewGuid().ToString("N");
        await using var db = _postgres.CreateContext();
        var men = db.Sports.Single(s => s.Slug == "tennis-men").Id;
        var player = new Player { SportId = men, Name = $"Rename Me {id}" };
        db.Players.AddRange(player, new Player { SportId = men, Name = string.Format(taken, id) });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidRequestException>(() =>
            Admin(db).UpdatePlayerAsync(player.Id, new AdminPlayerUpdateDto { Name = string.Format(wanted, id) }));

        await using var check = _postgres.CreateContext();
        Assert.Equal($"Rename Me {id}", await check.Players.Where(p => p.Id == player.Id).Select(p => p.Name).SingleAsync());
    }

    [Fact]
    public async Task ARename_IsSaved_OnPostgres()
    {
        var id = Guid.NewGuid().ToString("N");
        await using var db = _postgres.CreateContext();
        var player = new Player { SportId = db.Sports.Single(s => s.Slug == "tennis-men").Id, Name = $"Ilie Nastase {id}" };
        db.Players.Add(player);
        await db.SaveChangesAsync();

        await Admin(db).UpdatePlayerAsync(player.Id, new AdminPlayerUpdateDto { Name = $"Ilie Năstase {id}" });

        await using var check = _postgres.CreateContext();
        Assert.Equal($"Ilie Năstase {id}", await check.Players.Where(p => p.Id == player.Id).Select(p => p.Name).SingleAsync());
    }
}
