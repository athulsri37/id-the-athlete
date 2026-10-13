using IdTheAthlete.Api.Models;
using IdTheAthlete.Api.Services;
using IdTheAthlete.Api.Tests.TestSupport;
using Microsoft.Extensions.Time.Testing;

namespace IdTheAthlete.Api.Tests.Postgres;

// The Stats tab's grouped queries, run by Postgres itself: the in-memory
// tests can't show that EF translates them, or that the answers match.
// Dated in 1980 so other tests' games in the shared database stay out of
// the range.
[Collection(PostgresCollection.Name)]
public class AdminStatsPostgresTests
{
    private static readonly DateOnly Today = new(1980, 5, 13);

    private readonly PostgresFixture _postgres;

    public AdminStatsPostgresTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact]
    public async Task AllFourViews_MatchTheHandWorkedAnswers_OnPostgres()
    {
        await using var db = _postgres.CreateContext();
        var men = db.Sports.Single(s => s.Slug == "tennis-men").Id;
        var women = db.Sports.Single(s => s.Slug == "tennis-women").Id;
        var menAthlete = new Player { SportId = men, Name = $"Stats Men {Guid.NewGuid():N}" };
        var womenAthlete = new Player { SportId = women, Name = $"Stats Women {Guid.NewGuid():N}" };
        db.Players.AddRange(menAthlete, womenAthlete);
        await db.SaveChangesAsync();
        StatsScenario.Seed(db, Today, men, women, menAthlete.Id, womenAthlete.Id);

        var stats = await new AdminStatsService(db, new FakeTimeProvider(StatsScenario.Noon(Today)))
            .GetStatsAsync(null, StatsScenario.FirstOfMonth(Today), Today.ToString("yyyy-MM-dd"));

        StatsScenario.AssertThisMonth(stats, Today, menAthlete.Name);
    }
}
