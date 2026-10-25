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

// Every change saved through the admin tool is logged with the admin key's
// name and the old and new values; nothing is logged for a change that
// didn't happen.
public class AdminAuditTests
{
    private const int PlayerId = 7;
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly GameDbContext _db;
    private readonly AdminService _admin;

    public AdminAuditTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = "tennis-men", Family = "tennis" });
        _db.AttributeDefinitions.AddRange(
            new AttributeDefinition { Id = 1, SportId = 1, Key = "country", Label = "Country", Type = AttributeType.Categorical },
            new AttributeDefinition { Id = 2, SportId = 1, Key = "career_titles", Label = "Career Titles", Type = AttributeType.Numeric });
        _db.Players.Add(new Player { Id = PlayerId, SportId = 1, Name = "Some Player" });
        _db.PlayerAttributeValues.AddRange(
            new PlayerAttributeValue { PlayerId = PlayerId, AttributeDefinitionId = 1, Value = "Spain" },
            new PlayerAttributeValue { PlayerId = PlayerId, AttributeDefinitionId = 2, Value = "3" });
        _db.AppSettings.Add(new AppSetting { Key = "ActiveTheme", Value = "retro" });
        _db.SaveChanges();

        var time = new FakeTimeProvider(Now);
        var settings = new SettingsService(_db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));
        _admin = new AdminService(_db, time, settings, new DifficultyService(TestGameServiceFactory.CreateRegistry(settings)),
            new AdminActor { Name = "Athul" });
    }

    private static AdminPlayerUpdateDto Edit(string country, string titles, bool overridden = false, string? difficulty = null) => new()
    {
        Attributes = new() { ["country"] = country, ["career_titles"] = titles },
        IsOverridden = overridden,
        DifficultyOverride = difficulty,
    };

    [Fact]
    public async Task APlayerEdit_IsLoggedWithTheAdminAndWhatChanged()
    {
        await _admin.UpdatePlayerAsync(PlayerId, Edit("France", "3", overridden: true, difficulty: "Hard"));

        var entry = Assert.Single(await _admin.GetRecentChangesAsync(10));
        Assert.Equal((Now.UtcDateTime, "Athul", "Player edited", "Men's Tennis: Some Player"), (entry.At, entry.Admin, entry.Action, entry.Target));
        Assert.Equal("Country: Spain → France; Difficulty override: none → Hard", entry.Changes);
    }

    [Fact]
    public async Task AStatRefreshEdit_IsLoggedAsAStatRefresh()
    {
        var edit = Edit("Spain", "4");
        edit.Source = "stat-refresh";

        await _admin.UpdatePlayerAsync(PlayerId, edit);

        var entry = Assert.Single(await _admin.GetRecentChangesAsync(10));
        Assert.Equal(("Athul", "Stat refresh"), (entry.Admin, entry.Action));
        Assert.Equal("Career Titles: 3 → 4", entry.Changes);
    }

    [Theory]
    [InlineData("Stat-Refresh")]
    [InlineData("control-room")]
    public async Task AnUnknownSource_IsRefusedAndChangesNothing(string source)
    {
        var edit = Edit("France", "4");
        edit.Source = source;

        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.UpdatePlayerAsync(PlayerId, edit));

        Assert.Empty(await _admin.GetRecentChangesAsync(10));
        var values = await _db.PlayerAttributeValues.Where(v => v.PlayerId == PlayerId).Select(v => v.Value).ToListAsync();
        Assert.Equal(new[] { "Spain", "3" }, values.OrderByDescending(v => v));
    }

    [Fact]
    public async Task SavingAPlayerWithNothingChanged_LogsNothing()
    {
        await _admin.UpdatePlayerAsync(PlayerId, Edit("Spain", "3"));

        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task ARefusedPlayerEdit_LogsNothing()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.UpdatePlayerAsync(PlayerId, Edit("France", "lots")));

        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task ASettingChange_IsLoggedWithOldAndNewValues()
    {
        await _admin.UpdateSettingAsync("ActiveTheme", "stadium");

        var entry = Assert.Single(await _admin.GetRecentChangesAsync(10));
        Assert.Equal(("Athul", "Setting changed", "ActiveTheme", "ActiveTheme: retro → stadium"), (entry.Admin, entry.Action, entry.Target, entry.Changes));
    }

    [Fact]
    public async Task ARefusedOrUnchangedSetting_LogsNothing()
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.UpdateSettingAsync("ActiveTheme", "stadum"));
        await _admin.UpdateSettingAsync("ActiveTheme", "retro");

        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task RecentChanges_AreNewestFirst_AndLimited()
    {
        await _admin.UpdateSettingAsync("ActiveTheme", "stadium");
        await _admin.UpdatePlayerAsync(PlayerId, Edit("France", "3"));
        await _admin.UpdatePlayerAsync(PlayerId, Edit("Italy", "3"));

        var entries = await _admin.GetRecentChangesAsync(2);

        Assert.Equal(new[] { "Country: France → Italy", "Country: Spain → France" }, entries.Select(e => e.Changes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task ALimitOutOfRange_IsRefused(int limit)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.GetRecentChangesAsync(limit));
    }
}
