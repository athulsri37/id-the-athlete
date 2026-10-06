using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
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
        return new SettingsService(NewContext(), NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(TimeProvider.System));
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
    public async Task Decimals_fall_back_to_built_in_values_and_log_when_the_database_is_unreachable()
    {
        var unreachable = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=x;Password=x;Timeout=3")
            .Options;
        var logger = new CapturingLogger<SettingsService>();
        var settings = new SettingsService(new GameDbContext(unreachable), logger, new SettingsFailureLogThrottle(TimeProvider.System));

        var result = await settings.GetDecimalsAsync(new[]
        {
            "CricketMatchesClosenessPercent", "CricketMatchesClosenessFloor",
            "CricketRunsClosenessPercent", "CricketRunsClosenessFloor",
            "CricketWicketsClosenessPercent", "CricketWicketsClosenessFloor",
            "SomeFutureSetting",
        });

        Assert.Equal(new Dictionary<string, decimal>
        {
            ["CricketMatchesClosenessPercent"] = 15m, ["CricketMatchesClosenessFloor"] = 20m,
            ["CricketRunsClosenessPercent"] = 15m, ["CricketRunsClosenessFloor"] = 500m,
            ["CricketWicketsClosenessPercent"] = 15m, ["CricketWicketsClosenessFloor"] = 15m,
        }, result);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.Contains("SomeFutureSetting", entry.Message);
    }

    [Fact]
    public async Task Flags_fall_back_to_their_real_values_and_log_when_the_database_is_unreachable()
    {
        var unreachable = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=x;Password=x;Timeout=3")
            .Options;
        var logger = new CapturingLogger<SettingsService>();
        var settings = new SettingsService(new GameDbContext(unreachable), logger, new SettingsFailureLogThrottle(TimeProvider.System));

        var expected = new Dictionary<string, bool>
        {
            ["CountryClosenessEnabled"] = true,
            ["CricketRoleClosenessEnabled"] = true,
            ["CricketBowlingStyleClosenessEnabled"] = true,
            ["AiTriviaEnabled"] = false,
            ["SomeFutureFlag"] = false,
        };
        foreach (var (key, value) in expected)
            Assert.Equal(value, await settings.IsEnabledAsync(key));

        Assert.Equal(expected.Count, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.Equal(LogLevel.Error, e.Level));
        Assert.All(logger.Entries, e => Assert.NotNull(e.Exception));
        foreach (var key in expected.Keys)
            Assert.Single(logger.Entries, e => e.Message.Contains($"setting {key} failed"));
        Assert.Contains("no built-in fallback", logger.Entries.Single(e => e.Message.Contains("SomeFutureFlag")).Message);
    }

    [Fact]
    public async Task Repeated_failures_log_once_per_key_per_window_without_changing_the_values_returned()
    {
        var broken = NewContext();
        broken.Dispose(); // every query on it now throws, like a failed read
        var time = new FakeTimeProvider(new DateTimeOffset(2030, 1, 15, 12, 0, 0, TimeSpan.Zero));
        var logger = new CapturingLogger<SettingsService>();
        var settings = new SettingsService(broken, logger, new SettingsFailureLogThrottle(time));
        string[] numericKeys = { "CricketRunsClosenessPercent", "CricketRunsClosenessFloor" };
        var expectedNumeric = new Dictionary<string, decimal> { ["CricketRunsClosenessPercent"] = 15m, ["CricketRunsClosenessFloor"] = 500m };

        // Two failures inside the window: both return the fallback, one log line each.
        Assert.True(await settings.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.True(await settings.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.Equal(expectedNumeric, await settings.GetDecimalsAsync(numericKeys));
        Assert.Equal(expectedNumeric, await settings.GetDecimalsAsync(numericKeys));
        Assert.Equal(2, logger.Entries.Count);
        Assert.Single(logger.Entries, e => e.Message.Contains("setting CountryClosenessEnabled failed"));
        Assert.Single(logger.Entries, e => e.Message.Contains("numeric settings failed"));

        // Still inside the window: nothing new logged, values unchanged.
        time.Advance(SettingsFailureLogThrottle.Window - TimeSpan.FromSeconds(1));
        Assert.True(await settings.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.Equal(2, logger.Entries.Count);

        // Window elapsed: the next failure of each logs again.
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await settings.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.Equal(expectedNumeric, await settings.GetDecimalsAsync(numericKeys));
        Assert.Equal(4, logger.Entries.Count);
        Assert.Equal(2, logger.Entries.Count(e => e.Message.Contains("setting CountryClosenessEnabled failed")));

        // Each line reports how many failures of that key went unlogged since its last line.
        var flagLines = logger.Entries.Select(e => e.Message).Where(m => m.Contains("CountryClosenessEnabled")).ToList();
        Assert.Equal("Reading setting CountryClosenessEnabled failed; using its built-in fallback value True.", flagLines[0]);
        Assert.Equal("Reading setting CountryClosenessEnabled failed; using its built-in fallback value True" +
                     " (2 failures suppressed since last log).", flagLines[1]);
        var numericLines = logger.Entries.Select(e => e.Message).Where(m => m.Contains("numeric settings failed")).ToList();
        Assert.DoesNotContain("suppressed", numericLines[0]);
        Assert.Equal("Reading numeric settings failed; using built-in fallback values for" +
                     " CricketRunsClosenessPercent, CricketRunsClosenessFloor (1 failure suppressed since last log).", numericLines[1]);
    }

    [Fact]
    public async Task Numeric_failure_message_only_includes_clauses_that_name_keys()
    {
        async Task<string> MessageFor(params string[] keys)
        {
            var broken = NewContext();
            broken.Dispose();
            var logger = new CapturingLogger<SettingsService>();
            await new SettingsService(broken, logger, new SettingsFailureLogThrottle(TimeProvider.System)).GetDecimalsAsync(keys);
            return Assert.Single(logger.Entries).Message;
        }

        var knownOnly = await MessageFor("CricketRunsClosenessPercent", "CricketRunsClosenessFloor");
        var both = await MessageFor("CricketRunsClosenessPercent", "SomeFutureSetting");
        var unknownOnly = await MessageFor("SomeFutureSetting");

        Assert.Equal("Reading numeric settings failed; using built-in fallback values for" +
                     " CricketRunsClosenessPercent, CricketRunsClosenessFloor.", knownOnly);
        Assert.Equal("Reading numeric settings failed; using built-in fallback values for CricketRunsClosenessPercent" +
                     " and treating SomeFutureSetting as not configured.", both);
        Assert.Equal("Reading numeric settings failed; treating SomeFutureSetting as not configured.", unknownOnly);
        Assert.All(new[] { knownOnly, both, unknownOnly }, m => Assert.DoesNotContain("  ", m));
    }

    [Fact]
    public async Task Theme_defaults_to_retro_only_when_the_row_is_missing()
    {
        Assert.Equal("retro", await Seed().GetThemeAsync());
        Assert.Equal("stadium", await new SettingsService(SeedFresh(("ActiveTheme", "stadium")), NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(TimeProvider.System)).GetThemeAsync());
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
            var admin = new SettingsService(adminDb, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(TimeProvider.System));
            await admin.UpdateAsync("CountryClosenessEnabled", "false");
            await admin.UpdateAsync("CricketRunsClosenessPercent", "25");
        }

        // The same reader instance sees the new values immediately.
        Assert.False(await reader.IsEnabledAsync("CountryClosenessEnabled"));
        Assert.Equal(25m, (await reader.GetDecimalsAsync(new[] { "CricketRunsClosenessPercent" }))["CricketRunsClosenessPercent"]);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception), exception));
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
