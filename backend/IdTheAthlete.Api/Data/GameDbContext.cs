using Microsoft.EntityFrameworkCore;
using IdTheAthlete.Api.Models;

namespace IdTheAthlete.Api.Data;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();
    public DbSet<PlayerAttributeValue> PlayerAttributeValues => Set<PlayerAttributeValue>();
    public DbSet<DailyPuzzle> DailyPuzzles => Set<DailyPuzzle>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<GameRecord> GameRecords => Set<GameRecord>();
    public DbSet<AdminAuditEntry> AdminAuditEntries => Set<AdminAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sport>()
            .HasIndex(s => s.Slug)
            .IsUnique();

        // Two tours of one family can't share a web address (/tennis/men).
        // Empty means "use the slug", so it's left out of the check.
        modelBuilder.Entity<Sport>()
            .HasIndex(s => new { s.Family, s.UrlName })
            .IsUnique()
            .HasFilter("\"UrlName\" <> ''");

        modelBuilder.Entity<AttributeDefinition>()
            .HasOne(a => a.Sport)
            .WithMany(s => s.Attributes)
            .HasForeignKey(a => a.SportId);

        // A sport has each clue column once. Also what the reference-data
        // migration's ON CONFLICT ("SportId", "Key") relies on: Postgres needs
        // a matching unique index for that clause to be valid.
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

        // A player's name is unique within a sport, not across sports, so two
        // tours can each have a player of the same name. Two different people
        // in the same sport get a disambiguated name (see CLAUDE.md).
        modelBuilder.Entity<Player>()
            .HasIndex(p => new { p.SportId, p.Name })
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

        // Anonymous gameplay stats (see GameRecord).
        modelBuilder.Entity<GameRecord>(record =>
        {
            record.Property(r => r.Mode).HasMaxLength(10);
            record.Property(r => r.Result).HasConversion<string>().HasMaxLength(10);

            // Deleting an athlete keeps the stats of games played on them.
            record.HasOne(r => r.MysteryPlayer)
                .WithMany()
                .HasForeignKey(r => r.MysteryPlayerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Stats are read per sport over a date range.
            record.HasIndex(r => new { r.SportId, r.StartedAt });
        });

        // The admin audit log is read newest first.
        modelBuilder.Entity<AdminAuditEntry>(entry =>
        {
            entry.Property(e => e.AdminName).HasMaxLength(100);
            entry.Property(e => e.Action).HasMaxLength(50);
            entry.HasIndex(e => e.At);
        });
    }
}
