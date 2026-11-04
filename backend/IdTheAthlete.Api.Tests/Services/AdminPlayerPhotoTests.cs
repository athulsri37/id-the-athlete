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

// A player's photo is a Wikimedia Commons file with its credit and licence,
// set and removed from the Control Room or the photo review tool, and every
// change is logged.
public class AdminPlayerPhotoTests
{
    private const int PlayerId = 7;

    private readonly GameDbContext _db;
    private readonly AdminService _admin;

    public AdminPlayerPhotoTests()
    {
        _db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Sports.Add(new Sport { Id = 1, Name = "Men's Tennis", Slug = "tennis-men", Family = "tennis" });
        _db.Players.Add(new Player { Id = PlayerId, SportId = 1, Name = "Some Player" });
        _db.SaveChanges();

        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        var settings = new SettingsService(_db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));
        _admin = new AdminService(_db, time, settings, new DifficultyService(TestGameServiceFactory.CreateRegistry(settings)),
            new AdminActor { Name = "Athul" });
    }

    private static AdminPlayerPhotoUpdateDto Photo(string file = "Some Player 2015.jpg", int focusX = 50, int focusY = 50, int zoom = 100) => new()
    {
        File = file,
        Credit = "Jane Doe",
        License = "CC BY-SA 4.0",
        FocusX = focusX,
        FocusY = focusY,
        Zoom = zoom,
    };

    [Fact]
    public async Task APlayerWithoutAPhoto_HasNone()
    {
        Assert.Null((await _admin.GetPlayerAsync(PlayerId)).Photo);
    }

    [Fact]
    public async Task SettingAPhoto_SavesItAndLogsIt()
    {
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo(focusX: 0, focusY: 30));

        var photo = (await _admin.GetPlayerAsync(PlayerId)).Photo!;
        Assert.Equal(("Some Player 2015.jpg", "Jane Doe", "CC BY-SA 4.0", 0, 30),
            (photo.File, photo.Credit, photo.License, photo.FocusX, photo.FocusY));

        var entry = Assert.Single(await _admin.GetRecentChangesAsync(10));
        Assert.Equal(("Athul", "Photo edited", "Men's Tennis: Some Player"), (entry.Admin, entry.Action, entry.Target));
        Assert.Equal(
            "Photo: none → Some Player 2015.jpg; Photo credit: none → Jane Doe; Photo licence: none → CC BY-SA 4.0; Photo centre: 50% 50% → 0% 30%",
            entry.Changes);
    }

    [Fact]
    public async Task APhotoFromTheReviewTool_IsLoggedAsAPhotoReview()
    {
        var photo = Photo();
        photo.Source = "photo-review";

        await _admin.SetPlayerPhotoAsync(PlayerId, photo);

        Assert.Equal("Photo review", Assert.Single(await _admin.GetRecentChangesAsync(10)).Action);
    }

    [Fact]
    public async Task MovingTheCentre_LogsOnlyThat()
    {
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo());
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo(focusX: 40, focusY: 20));

        var latest = (await _admin.GetRecentChangesAsync(10))[0];
        Assert.Equal("Photo centre: 50% 50% → 40% 20%", latest.Changes);
    }

    [Fact]
    public async Task ZoomingIn_SavesItAndLogsOnlyThat()
    {
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo());
        Assert.Equal(100, (await _admin.GetPlayerAsync(PlayerId)).Photo!.Zoom);

        await _admin.SetPlayerPhotoAsync(PlayerId, Photo(zoom: 180));

        Assert.Equal(180, (await _admin.GetPlayerAsync(PlayerId)).Photo!.Zoom);
        Assert.Equal("Photo zoom: 100% → 180%", (await _admin.GetRecentChangesAsync(10))[0].Changes);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(301)]
    [InlineData(0)]
    public async Task AZoomOutside1To3Times_IsRefused(int zoom)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.SetPlayerPhotoAsync(PlayerId, Photo(zoom: zoom)));
    }

    [Fact]
    public async Task SavingTheSamePhotoAgain_LogsNothing()
    {
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo());
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo());

        Assert.Single(await _admin.GetRecentChangesAsync(10));
    }

    [Theory]
    [InlineData("File:Some_Player_2015.jpg", "Some Player 2015.jpg")]
    [InlineData("  Some Player.JPEG ", "Some Player.JPEG")]
    [InlineData("Logo.svg", "Logo.svg")]
    public async Task AFileName_IsStoredTheWayCommonsShowsIt(string typed, string stored)
    {
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo(typed));

        Assert.Equal(stored, (await _admin.GetPlayerAsync(PlayerId)).Photo!.File);
    }

    [Theory]
    [InlineData("")]
    [InlineData("File:")]
    [InlineData("Some Player")] // no image extension
    [InlineData("Some Player.pdf")]
    [InlineData("folder/Some Player.jpg")]
    [InlineData("Some [Player].jpg")]
    public async Task ABadFileName_IsRefusedAndChangesNothing(string file)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.SetPlayerPhotoAsync(PlayerId, Photo(file)));

        Assert.Null((await _admin.GetPlayerAsync(PlayerId)).Photo);
        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }

    [Fact]
    public async Task AMissingCreditOrLicence_IsRefused()
    {
        var noCredit = Photo();
        noCredit.Credit = "  ";
        var noLicence = Photo();
        noLicence.License = "";

        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.SetPlayerPhotoAsync(PlayerId, noCredit));
        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.SetPlayerPhotoAsync(PlayerId, noLicence));
    }

    [Theory]
    [InlineData(-1, 50)]
    [InlineData(50, 101)]
    public async Task ACentreOutsideThePhoto_IsRefused(int focusX, int focusY)
    {
        await Assert.ThrowsAsync<InvalidRequestException>(
            () => _admin.SetPlayerPhotoAsync(PlayerId, Photo(focusX: focusX, focusY: focusY)));
    }

    [Theory]
    [InlineData("Photo-Review")]
    [InlineData("stat-refresh")]
    public async Task AnUnknownSource_IsRefused(string source)
    {
        var photo = Photo();
        photo.Source = source;

        await Assert.ThrowsAsync<InvalidRequestException>(() => _admin.SetPlayerPhotoAsync(PlayerId, photo));
    }

    [Fact]
    public async Task AnUnknownPlayer_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _admin.SetPlayerPhotoAsync(999, Photo()));
        await Assert.ThrowsAsync<NotFoundException>(() => _admin.RemovePlayerPhotoAsync(999));
    }

    [Fact]
    public async Task RemovingAPhoto_ClearsItAndLogsIt()
    {
        await _admin.SetPlayerPhotoAsync(PlayerId, Photo(focusX: 10, zoom: 250));

        await _admin.RemovePlayerPhotoAsync(PlayerId);

        Assert.Null((await _admin.GetPlayerAsync(PlayerId)).Photo);
        var player = await _db.Players.AsNoTracking().SingleAsync(p => p.Id == PlayerId);
        Assert.Equal((null, null, 50, 50, 100), (player.PhotoCredit, player.PhotoLicense, player.PhotoFocusX, player.PhotoFocusY, player.PhotoZoom));
        var latest = (await _admin.GetRecentChangesAsync(10))[0];
        Assert.Equal(("Photo removed", "Photo: Some Player 2015.jpg → none"), (latest.Action, latest.Changes));
    }

    [Fact]
    public async Task RemovingAPhotoThePlayerDoesntHave_LogsNothing()
    {
        await _admin.RemovePlayerPhotoAsync(PlayerId);

        Assert.Empty(await _admin.GetRecentChangesAsync(10));
    }
}
