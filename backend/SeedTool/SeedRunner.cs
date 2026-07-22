using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Data;

namespace SeedTool;

// The reusable core of the seed tool: discover every .sql file under a
// seed data root and execute them as upserts against the database, in a
// deterministic order. Deliberately separate from Program.cs/Main so this
// can be called directly by something other than the CLI later (e.g. an
// automated sync job) without going through argument parsing or a process
// boundary.
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