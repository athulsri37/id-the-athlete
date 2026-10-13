using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Rules;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdTheAthlete.Api.Tests.TestSupport;

// The one place tests build a real GameService with its real collaborators
// (nothing mocked), so a change to its dependencies is a change here
// rather than in every test class.
internal static class TestGameServiceFactory
{
    public static GameService Create(
        GameDbContext db,
        IPracticeSessionService sessions,
        IGameTokenService? tokens = null,
        ILogger<GameService>? logger = null,
        TimeProvider? time = null)
    {
        time ??= TimeProvider.System;
        var settings = new SettingsService(db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));

        var sportRules = CreateRegistry(settings);

        return new GameService(
            db,
            new DifficultyService(sportRules),
            sportRules,
            sessions,
            new DailyPuzzleService(db, NullLogger<DailyPuzzleService>.Instance, time),
            tokens ?? GameTokenServiceTests.Create(),
            new GameStatsService(db, time, NullLogger<GameStatsService>.Instance),
            logger ?? NullLogger<GameService>.Instance);
    }

    // The production set of sport rules, as registered in Program.cs.
    public static SportRulesRegistry CreateRegistry(ISettingsService settings) =>
        new(new ISportRules[] { new TennisRules(settings), new CricketRules(settings) });
}
