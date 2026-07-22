using Microsoft.EntityFrameworkCore;
using TennisGuessr.Api.Models;

namespace TennisGuessr.Api.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<PlayerAttributeValue> PlayerAttributeValues => Set<PlayerAttributeValue>();
    public DbSet<DailyPuzzle> DailyPuzzles => Set<DailyPuzzle>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sport>()
            .HasIndex(s => s.Slug)
            .IsUnique();

        modelBuilder.Entity<AttributeDefinition>()
            .HasOne(a => a.Sport)
            .WithMany(s => s.Attributes)
            .HasForeignKey(a => a.SportId);

        // Required for the SeedTool's "ON CONFLICT (SportId, Key)" upsert
        // pattern against AttributeDefinitions — Postgres needs a matching
        // unique constraint/index for that clause to be valid.
        modelBuilder.Entity<AttributeDefinition>()
            .HasIndex(a => new { a.SportId, a.Key })
            .IsUnique();

        modelBuilder.Entity<Player>()
            .HasOne(p => p.Sport)
            .WithMany(s => s.Players)
            .HasForeignKey(p => p.SportId);

        modelBuilder.Entity<Player>()
            .Property(p => p.IsOverridden)
            .HasDefaultValue(false);

        // Key used for the SeedTool's upsert matching (ON CONFLICT "Name").
        modelBuilder.Entity<Player>()
            .HasIndex(p => p.Name)
            .IsUnique();

        modelBuilder.Entity<PlayerAttributeValue>()
            .HasOne(v => v.Player)
            .WithMany(p => p.AttributeValues)
            .HasForeignKey(v => v.PlayerId);

        modelBuilder.Entity<PlayerAttributeValue>()
            .HasOne(v => v.AttributeDefinition)
            .WithMany(a => a.Values)
            .HasForeignKey(v => v.AttributeDefinitionId);

        modelBuilder.Entity<PlayerAttributeValue>()
            .HasIndex(v => new { v.PlayerId, v.AttributeDefinitionId })
            .IsUnique();

        modelBuilder.Entity<DailyPuzzle>()
            .HasIndex(d => new { d.SportId, d.PuzzleDate })
            .IsUnique();

        modelBuilder.Entity<AppSetting>()
            .HasIndex(s => s.Key)
            .IsUnique();
    }
}
