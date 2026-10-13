namespace IdTheAthlete.Api.Tests.Postgres;

// Every test class marked [Collection(PostgresCollection.Name)] shares one
// PostgresFixture, so the container starts once per test run.
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
