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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sport>()
            .HasIndex(s => s.Slug)
            .IsUnique();

        modelBuilder.Entity<AttributeDefinition>()
            .HasOne(a => a.Sport)
            .WithMany(s => s.Attributes)
            .HasForeignKey(a => a.SportId);

        modelBuilder.Entity<Player>()
            .HasOne(p => p.Sport)
            .WithMany(s => s.Players)
            .HasForeignKey(p => p.SportId);

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
    }
}
