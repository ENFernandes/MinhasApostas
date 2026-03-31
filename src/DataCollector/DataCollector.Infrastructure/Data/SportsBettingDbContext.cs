using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Infrastructure.Jobs;

namespace SportsBetting.DataCollector.Infrastructure.Data;

/// <summary>
/// Entity Framework database context for the sports betting application.
/// </summary>
public class SportsBettingDbContext : DbContext
{
    public SportsBettingDbContext(DbContextOptions<SportsBettingDbContext> options)
        : base(options)
    {
    }

    public DbSet<CompetitionEntity> Competitions => Set<CompetitionEntity>();
    public DbSet<TeamEntity> Teams => Set<TeamEntity>();
    public DbSet<PlayerEntity> Players => Set<PlayerEntity>();
    public DbSet<MatchEntity> Matches => Set<MatchEntity>();
    public DbSet<OddsEntity> Odds => Set<OddsEntity>();
    public DbSet<RecommendationEntity> Recommendations => Set<RecommendationEntity>();
    public DbSet<BetEntity> Bets => Set<BetEntity>();
    public DbSet<MatchLineupEntity> MatchLineups => Set<MatchLineupEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Competition configuration
        modelBuilder.Entity<CompetitionEntity>(entity =>
        {
            entity.ToTable("competitions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Sport).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.Season).HasMaxLength(20);
            entity.Ignore(e => e.IsActive);
        });

        // Team configuration
        modelBuilder.Entity<TeamEntity>(entity =>
        {
            entity.ToTable("teams");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ExternalId, e.Sport }).IsUnique();
            entity.Property(e => e.Sport).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ShortName).HasMaxLength(50);
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        // Player configuration
        modelBuilder.Entity<PlayerEntity>(entity =>
        {
            entity.ToTable("players");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalId);
            entity.Property(e => e.Sport).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100);
        });

        // Match configuration
        modelBuilder.Entity<MatchEntity>(entity =>
        {
            entity.ToTable("matches");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ExternalId, e.Sport }).IsUnique();
            entity.HasIndex(e => e.CommenceTime);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Sport).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();

            // Relationships
            entity.HasOne(m => m.Competition)
                .WithMany()
                .HasForeignKey(m => m.CompetitionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(m => m.HomeTeam)
                .WithMany()
                .HasForeignKey(m => m.HomeId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.AwayTeam)
                .WithMany()
                .HasForeignKey(m => m.AwayId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Odds configuration
        modelBuilder.Entity<OddsEntity>(entity =>
        {
            entity.ToTable("odds");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.MatchId, e.Bookmaker, e.Market });
            entity.Property(e => e.Bookmaker).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Market).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Outcome).HasMaxLength(50).IsRequired();
        });

        // Recommendations configuration
        modelBuilder.Entity<RecommendationEntity>(entity =>
        {
            entity.ToTable("recommendations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MatchId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Market).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Outcome).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Bookmaker).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reasoning).HasColumnType("text");
            entity.HasOne(e => e.Match)
                .WithMany()
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Bets configuration
        modelBuilder.Entity<BetEntity>(entity =>
        {
            entity.ToTable("bets");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RecommendationId);
            entity.HasIndex(e => e.MatchId);
            entity.HasIndex(e => e.Result);
            entity.Property(e => e.Market).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Outcome).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Bookmaker).HasMaxLength(100);
            entity.Property(e => e.Result).HasMaxLength(20);
            entity.HasOne(e => e.Match)
                .WithMany()
                .HasForeignKey(e => e.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Recommendation)
                .WithOne(r => r.Bet)
                .HasForeignKey<BetEntity>(e => e.RecommendationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // MatchLineup configuration (V011 migration)
        modelBuilder.Entity<MatchLineupEntity>(entity =>
        {
            entity.ToTable("match_lineups");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MatchId);
            entity.HasIndex(e => new { e.MatchId, e.TeamId }).IsUnique();
            entity.Property(e => e.Formation).HasMaxLength(10);
            entity.Property(e => e.Players).HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.Substitutes).HasColumnType("jsonb").IsRequired();
        });
    }
}
