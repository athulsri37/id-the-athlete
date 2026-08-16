using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Data;

namespace SeedTool;

// The reusable core of the seed tool: discover every .sql file under a
// seed data root and execute them as upserts against the database, in a
// deterministic order. Deliberately separate from Program.cs/Main so this
// can be called directly by something other than the CLI later (e.g. an
// automated sync job) without going through argument parsing or a process
// boundary.
//
// EVERY file runs on EVERY invocation, not just newly-added ones -- there's
// no "already seeded" tracking. That's fine for Players/AttributeDefinitions
// (their upserts are idempotent no-ops once the data matches), but it means
// a PlayerAttributeValues row a curator has since hand-edited via
// /control-room would get silently overwritten back to its original seeded
// value on the next run of ANY batch file, not just the one that first
// seeded that player. Each seed file's ON CONFLICT DO UPDATE therefore
// guards on PlayerAttributeValues.IsManuallyEdited = false, so a manual
// correction (see AdminService.UpdatePlayerAsync) sticks permanently.
//
// The same hazard applied to Players.IsOverridden/DifficultyOverride
// (curator-set difficulty overrides), which every seed file's Players
// upsert used to stomp back to false/NULL on every re-run too. No seed
// file ever sets IsOverridden = true except as a deliberate, permanent
// curatorial baseline (see the one existing example, Grigor Dimitrov in
// Tennis/Men/players-batch-02.sql) -- so unlike PlayerAttributeValues,
// Players.IsOverridden doubles as its own "has this been manually set"
// flag with no extra column needed: each seed file's Players upsert now
// keeps the existing row's IsOverridden/DifficultyOverride whenever it's
// already true (via a CASE expression, not a WHERE-guarded DO UPDATE, so
// SportId still always stays in sync regardless).
public static class SeedRunner
{
    public static async Task<IReadOnlyList<string>> RunAsync(GameDbContext db, string seedDataRoot)
    {
        if (!Directory.Exists(seedDataRoot))
            throw new DirectoryNotFoundException($"Seed data directory not found: {seedDataRoot}");

        var files = DiscoverSeedFiles(seedDataRoot);

        await using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var file in files)
        {
            var sql = await File.ReadAllTextAsync(file);
            if (string.IsNullOrWhiteSpace(sql))
                continue;

            await db.Database.ExecuteSqlRawAsync(sql);
        }

        await transaction.CommitAsync();

        return files.Select(f => Path.GetRelativePath(seedDataRoot, f)).ToList();
    }

    // Sorting by relative path (not just filename) keeps ordering
    // predictable across subdirectories while still relying on the
    // "00-..." / "players-batch-NN..." naming convention: attribute-
    // definition files always sort before player batches, and batches sort
    // in numeric order as long as the numeric suffix stays zero-padded.
    private static List<string> DiscoverSeedFiles(string seedDataRoot)
    {
        return Directory
            .GetFiles(seedDataRoot, "*.sql", SearchOption.AllDirectories)
            .OrderBy(f => Path.GetRelativePath(seedDataRoot, f), StringComparer.Ordinal)
            .ToList();
    }
}