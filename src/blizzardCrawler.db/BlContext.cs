using Microsoft.EntityFrameworkCore;

namespace blizzardCrawler.db;

public class BlContext : DbContext
{
    public virtual DbSet<Player> Players { get; set; }
    public virtual DbSet<MatchInfo> MatchInfos { get; set; }

    public BlContext(DbContextOptions<BlContext> options)
    : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasIndex(e => new { e.ToonId, e.RegionId, e.RealmId }).IsUnique();
        });

        modelBuilder.Entity<MatchInfo>(entity =>
        {
            entity.HasIndex(e => e.MatchDateUnixTimestamp);
            entity.HasIndex(e => new { e.PlayerId, e.MatchDateUnixTimestamp, e.Region, e.Decision })
                .IsUnique();
        });
    }
}
