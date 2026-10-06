using System.Globalization;
using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace IdTheAthlete.Api.Tests.Services;

public class SettingsServiceTests
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    private GameDbContext NewContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>().UseInMemoryDatabase(_dbName).Options);

    private SettingsService Seed(params (string Key, string Value)[] rows)
    {
        using (var db = NewContext())
        {
            foreach (var (key, value) in rows)
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            db.SaveChanges();
        }
        return new SettingsService(NewContext());
    }

    [Fact]
    public async Task A_flag_is_on_only_when_its_value_is_exactly_true()
    {
        var settings = Seed(("Lower", "true"), ("Capital", "True"), ("Upper", "TRUE"), ("Padded", " true"), ("Off", "false"));

        Assert.True(await settings.IsEnabledAsync("Lower"));
        Assert.False(await settings.IsEnabledAsync("Capital"));
        Assert.False(await settings.IsEnabledAsync("Upper"));
        Assert.False(await settings.IsEnabledAsync("Padded"));
        Assert.False(await settings.IsEnabledAsync("Off"));
        Assert.False(await settings.IsEnabledAsync("Missing"));
    }

    [Fact]
    public async Task Decimals_skip_missing_and_unparseable_values_in_one_batch()
    {
        var settings = Seed(("Percent", "15"), ("Floor", "500"), ("Junk", "lots"), ("Empty", ""));

        var result = await settings.GetDecimalsAsync(new[] { "Percent", "Floor", "Junk", "Empty", "Missing" });

        Assert.Equal(new Dictionary<string, decimal> { ["Percent"] = 15m, ["Floor"] = 500m }, result);
    }

    [Fact]
    public async Task Decimals_parse_the_same_under_a_culture_that_uses_a_decimal_comma()
    {
        var settings = Seed(("Fraction", "2.5"), ("Percent", "15"), ("Floor", "500"));
        var original = CultureInfo.CurrentCulture;
        try
        {
            // In de-DE "." is the thousands separator, so culture-sensitive
            // parsing silently turns "2.5" into 25.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.True(decimal.TryParse("2.5", out var cultureSensitive));
            Assert.Equal(25m, cultureSensitive);

            var result = await settings.GetDecimalsAsync(new[] { "Fraction", "Percent", "Floor" });

            Assert.Equal(new Dictionary<string, decimal> { ["Fraction"] = 2.5m, ["Percent"] = 15m, ["Floor"] = 500m }, result);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task Theme_defaults_to_retro_only_when_the_row_is_missing()
    {
        Assert.Equal("retro", await Seed().GetThemeAsync());
        Assert.Equal("stadium", await new SettingsService(SeedFresh(("ActiveTheme", "stadium"))).GetThemeAsync());
    }

    [Fact]
    public async Task Update_rejects_unknown_keys_and_non_boolean_values_for_boolean_settings()
    {
        var settings = Seed(("Flag", "true"), ("Theme", "retro"));

        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() => settings.UpdateAsync("Nope", "x"));
        Assert.Equal("Setting 'Nope' not found.", unknown.Message);

        var notBoolean = await Assert.ThrowsAsync<InvalidOperationException>(() => settings.UpdateAsync("Flag", "maybe"));
        Assert.Equal("Setting 'Flag' is boolean; value must be 'true' or 'false'.", notBoolean.Message);

        await settings.UpdateAsync("Flag", "false");
        await settings.UpdateAsync("Theme", "stadium");
        Assert.Equal(
            new[] { new KeyValuePair<string, string>("Flag", "false"), new KeyValuePair<string, string>("Theme", "stadium") },
            await settings.GetAllAsync());
    }

    [Fact]
    public async Task A_change_saved_elsewhere_is_visible_on_the_next_read_with_no_caching()
    {
        var reader = Seed(("CountryClosenessEnabled", "true"), ("CricketRunsClosenessPercent", "15"));
        Assert.True(await reader.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.Equal(15m, (await reader.GetDecimalsAsync(new[] { "CricketRunsClosenessPercent" }))["CricketRunsClosenessPercent"]);

        // An admin request on a different context changes both values.
        using (var adminDb = NewContext())
        {
            var admin = new SettingsService(adminDb);
            await admin.UpdateAsync("CountryClosenessEnabled", "false");
            await admin.UpdateAsync("CricketRunsClosenessPercent", "25");
        }

        // The same reader instance sees the new values immediately.
        Assert.False(await reader.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.Equal(25m, (await reader.GetDecimalsAsync(new[] { "CricketRunsClosenessPercent" }))["CricketRunsClosenessPercent"]);
    }

    private GameDbContext SeedFresh(params (string Key, string Value)[] rows)
    {
        var db = new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        foreach (var (key, value) in rows)
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        db.SaveChanges();
        return db;
    }
}
