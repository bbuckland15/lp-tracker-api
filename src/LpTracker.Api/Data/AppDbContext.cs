using LpTracker.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LpTracker.Api.Data;

/// <summary>
/// Entity Framework Core's view of the database: one DbSet per table,
/// plus the keys, indexes and relationships configured below.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<LpChange> LpChanges => Set<LpChange>();
    public DbSet<LpSnapshot> LpSnapshots => Set<LpSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(p =>
        {
            p.Property(x => x.Name).IsRequired().HasMaxLength(50);
            p.Property(x => x.RiotId).HasMaxLength(40);
            p.Property(x => x.Puuid).HasMaxLength(100);

            // No two players with the same name, and one Riot account can't be linked twice.
            p.HasIndex(x => x.Name).IsUnique();
            p.HasIndex(x => x.Puuid).IsUnique();
        });

        modelBuilder.Entity<Match>(m =>
        {
            m.Property(x => x.RiotMatchId).HasMaxLength(30);

            // The same Riot game is never imported twice. (Manual matches have no
            // RiotMatchId; NULLs don't count as duplicates.)
            m.HasIndex(x => x.RiotMatchId).IsUnique();
        });

        modelBuilder.Entity<LpSnapshot>(s =>
        {
            s.Property(x => x.Tier).IsRequired().HasMaxLength(20);
            s.Property(x => x.Division).IsRequired().HasMaxLength(5);

            s.HasOne(x => x.Player)
             .WithMany(p => p.Snapshots)
             .HasForeignKey(x => x.PlayerId)
             .OnDelete(DeleteBehavior.Cascade);

            // The sync always asks for "this player's latest snapshot".
            s.HasIndex(x => new { x.PlayerId, x.TakenAt });
        });

        modelBuilder.Entity<LpChange>(c =>
        {
            // One player -> many LP changes. Deleting a player is blocked while they have history,
            // so we never silently lose results.
            c.HasOne(x => x.Player)
             .WithMany(p => p.LpChanges)
             .HasForeignKey(x => x.PlayerId)
             .OnDelete(DeleteBehavior.Restrict);

            // One match -> many LP changes. Deleting a match (e.g. entered by mistake)
            // removes its LP changes with it.
            c.HasOne(x => x.Match)
             .WithMany(m => m.LpChanges)
             .HasForeignKey(x => x.MatchId)
             .OnDelete(DeleteBehavior.Cascade);

            // A player can only have one result per match.
            // This composite index also serves lookups by MatchId.
            c.HasIndex(x => new { x.MatchId, x.PlayerId }).IsUnique();

            // Standings and history both filter/group by PlayerId, so index it.
            c.HasIndex(x => x.PlayerId);
        });
    }
}
