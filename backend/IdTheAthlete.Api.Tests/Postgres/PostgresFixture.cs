using IdTheAthlete.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace IdTheAthlete.Api.Tests.Postgres;

// One throwaway Postgres for every test in the Postgres collection, started
// in Docker with the same image as docker-compose and migrated with the
// real migrations. The in-memory database used elsewhere doesn't enforce
// unique indexes or foreign keys; these tests check what only Postgres does.
// Tests share the database, so each one uses its own names and dates.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public GameDbContext CreateContext() => new(new DbContextOptionsBuilder<GameDbContext>()
        .UseNpgsql(_container.GetConnectionString())
        .Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
