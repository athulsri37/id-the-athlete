using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Dtos;
using IdTheAthlete.Api.Errors;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Services;

// Renaming a player in the Control Room: the name is trimmed, must be
// unique in its tour (ignoring case), and the rename is logged; leaving the
// name out keeps it.
public class AdminPlayerRenameTests
{
    private const int PlayerId = 7;
    private const int OtherPlayerId = 8;
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly GameDbContext _db;
    private readonly AdminService _admin;

    public AdminPlayerRenameTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.AddRange(
            new Sport { Id = 1, Name = "Men's Tennis", Slug = "tennis-men", Family = "tennis" },
            new Sport { Id = 2, Name = "Women's Tennis", Slug = "tennis-women", Family = "tennis" });
        _db.Players.AddRange(
            new Player { Id = PlayerId, SportId = 1, Name = "Ilie Nastase" },
            new Player { Id = OtherPlayerId, SportId = 1, Name = "Borna Ćorić" },
            new Player { Id = 9, SportId = 2, Name = "Kim Clijsters" });
        _db.SaveChanges();

        var time = new FakeTimeProvider(Now);
        var settings = new SettingsService(_db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));
        _admin = new AdminService(_db, time, settings, new DifficultyService(TestGameServiceFactory.CreateRegistry(settings)),
            new AdminActor { Name = "Athul" });
    }

    private static AdminPlayerUpdateDto Rename(string? name) => new() { Name = name };

    private async Task<Player> PlayerAsync() => await _db.Players.AsNoTracking().SingleAsync(p => p.Id == PlayerId);

    [Fact]
    public async Task ARename_IsSavedTrimmed_AndLoggedWithTheOldAndNewName()
    {
        await _admin.UpdatePlayerAsync(PlayerId, Rename("  Ilie Năstase "));

        var player = await PlayerAsync();
        Assert.Equal("Ilie Năstase", player.Name);
        // The trivia blurb is written from the name, so it's regenerated.
        Assert.Equal(Now.UtcDateTime, player.LastModifiedAt);

        var entry = Assert.Single(await _admin.GetRecentChangesAsync(10));
        Assert.Equal(("Player edited", "Men's Tennis: Ilie Năstase", "Name: Ilie Nastase → Ilie Năstase"),
            (entry.Action, entry.Target, entry.Changes));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Ilie Nastase")]
    [InlineData(" Ilie Nastase ")]
    public async Task NoNameOrTheSameName_KeepsItAndLogsNothing(string? name)
    {
        await _admin.UpdatePlayerAsync(PlayerId, Rename(name));

        var player = await PlayerAsync();
        Assert.Equal(("Ilie Nastase", (DateTime?)null), (player.Name, player.LastModifiedAt));
        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task OnlyChangingTheCapitals_IsAllowed()
    {
        await _admin.UpdatePlayerAsync(PlayerId, Rename("ILIE NASTASE"));

        Assert.Equal("ILIE NASTASE", (await PlayerAsync()).Name);
    }

    [Theory]
    [InlineData("Borna Ćorić")]
    [InlineData("borna ćorić")]
    public async Task ANameAnotherPlayerInTheTourHas_IsRefusedAndChangesNothing(string name)
    {
        var ex = await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.UpdatePlayerAsync(PlayerId, Rename(name)));

        Assert.Contains("already called", ex.Message);
        Assert.Equal("Ilie Nastase", (await PlayerAsync()).Name);
        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task ANameOnlyAPlayerInAnotherTourHas_IsAllowed()
    {
        await _admin.UpdatePlayerAsync(PlayerId, Rename("Kim Clijsters"));

        Assert.Equal("Kim Clijsters", (await PlayerAsync()).Name);
    }

    [Fact]
    public async Task AnEmptyOrTooLongName_IsRefusedAndChangesNothing()
    {
        foreach (var name in new[] { "", "   ", new string('a', 101) })
            await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.UpdatePlayerAsync(PlayerId, Rename(name)));

        Assert.Equal("Ilie Nastase", (await PlayerAsync()).Name);
        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task ANameOfExactlyTheLimit_IsAllowed()
    {
        var name = new string('a', 100);

        await _admin.UpdatePlayerAsync(PlayerId, Rename(name));

        Assert.Equal(name, (await PlayerAsync()).Name);
    }

    [Fact]
    public async Task ARenameWithAClueChange_IsLoggedAsOneEdit()
    {
        _db.AttributeDefinitions.Add(new AttributeDefinition { Id = 1, SportId = 1, Key = "country", Label = "Country", Type = AttributeType.Categorical });
        _db.PlayerAttributeValues.Add(new PlayerAttributeValue { PlayerId = PlayerId, AttributeDefinitionId = 1, Value = "Romania" });
        await _db.SaveChangesAsync();

        await _admin.UpdatePlayerAsync(PlayerId, new AdminPlayerUpdateDto
        {
            Name = "Ilie Năstase",
            Attributes = new() { ["country"] = "France" },
        });

        var entry = Assert.Single(await _admin.GetRecentChangesAsync(10));
        Assert.Equal("Name: Ilie Nastase → Ilie Năstase; Country: Romania → France", entry.Changes);
    }
}
