using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Data;

namespace SeedTool;

// Standalone seeding tool, fully decoupled from the web app's startup.
// Invoke manually:
//   dotnet run --project SeedTool -- --connection "Host=localhost;Database=tennisguessr;Username=tennisguessr;Password=tennisguessr_dev_pw"
// Never run automatically — the web app only migrates on boot (see
// TennisGuessr.Api/Program.cs).
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var connectionString = GetArgValue(args, "--connection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.Error.WriteLine("Usage: dotnet run --project SeedTool -- --connection \"<postgres-connection-string>\"");
            return 1;
        }

        var seedDataRoot = Path.Combine(AppContext.BaseDirectory, "SeedData");

        var optionsBuilder = new DbContextOptionsBuilder<GameDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        await using var db = new GameDbContext(optionsBuilder.Options);

        try
        {
            var executedFiles = await SeedRunner.RunAsync(db, seedDataRoot);

            Console.WriteLine($"Seeded successfully. Executed {executedFiles.Count} file(s):");
            foreach (var file in executedFiles)
                Console.WriteLine($"  {file}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Seeding failed: {ex.Message}");
            return 1;
        }
    }

    private static string? GetArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }
}