using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Rules;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IdTheAthlete.Api.Tests.TestSupport;

// The one place tests build a real GameService with its real collaborators
// (only AI trivia is mocked, by default), so a change to its dependencies is a change here
// rather than in every test class.
internal static class TestGameServiceFactory
{
    public static GameService Create(
        GameDbContext db,
        IPracticeSessionService sessions,
        IGameTokenService? tokens = null,
        ILogger<GameService>? logger = null,
        TimeProvider? time = null,
        IAiTriviaService? trivia = null)
    {
        time ??= TimeProvider.System;
        var settings = new SettingsService(db, NullLogger<SettingsService>.Instance, new SettingsFailureLogThrottle(time));

        var sportRules = CreateRegistry(settings);

        return new GameService(
            db,
            trivia ?? Mock.Of<IAiTriviaService>(),
            new DifficultyService(sportRules),
            sportRules,
            sessions,
            new DailyPuzzleService(db, NullLogger<DailyPuzzleService>.Instance, time),
            tokens ?? GameTokenServiceTests.Create(),
            logger ?? NullLogger<GameService>.Instance);
    }

    // The production set of sport rules, as registered in Program.cs.
    public static SportRulesRegistry CreateRegistry(ISettingsService settings) =>
        new(new ISportRules[] { new TennisRules(settings), new CricketRules(settings) });
}
