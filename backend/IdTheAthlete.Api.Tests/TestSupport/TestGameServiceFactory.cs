using IdTheAthlete.Api.Data;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace IdTheAthlete.Api.Tests.TestSupport;

// The one place tests build a real GameService with its real collaborators
// (only AI trivia is mocked), so a change to its dependencies is a change here
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

        return new GameService(
            db,
            Mock.Of<IAiTriviaService>(),
            new DifficultyService(),
            new NumericClosenessEvaluator(settings),
            new CategoricalClosenessEvaluator(settings),
            sessions,
            new DailyPuzzleService(db, NullLogger<DailyPuzzleService>.Instance, time),
            tokens ?? GameTokenServiceTests.Create(),
            logger ?? NullLogger<GameService>.Instance);
    }
}
