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
    public DbSet<PlayerEloHistoryEntity> PlayerEloHistory => Set<PlayerEloHistoryEntity>();
    public DbSet<LatestPlayerEloEntity> LatestPlayerElo => Set<LatestPlayerEloEntity>();
    public DbSet<HistoricalTennisMatchEntity> HistoricalTennisMatches => Set<HistoricalTennisMatchEntity>();
    public DbSet<FootballStandingEntity> FootballStandings => Set<FootballStandingEntity>();
    public DbSet<TennisRankingEntity> TennisRankings => Set<TennisRankingEntity>();

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
            entity.Property(e => e.NameNormalized).HasColumnName("name_normalized");
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
            entity.Property(e => e.ManualEventLabel).HasMaxLength(500);
            entity.Property(e => e.ManualSport).HasMaxLength(32);
            entity.HasOne(e => e.Match)
                .WithMany()
                .HasForeignKey(e => e.MatchId)
                .IsRequired(false)
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

        // Historical tennis ELO snapshots (seeded)
        modelBuilder.Entity<PlayerEloHistoryEntity>(entity =>
        {
            entity.ToTable("player_elo_history");
            entity.HasNoKey();
            entity.Property(e => e.PlayerName).HasColumnName("player_name");
            entity.Property(e => e.MatchDate).HasColumnName("match_date");
        });

        // View: latest_player_elo (keyless)
        modelBuilder.Entity<LatestPlayerEloEntity>(entity =>
        {
            entity.ToView("latest_player_elo");
            entity.HasNoKey();
            entity.Property(e => e.PlayerName).HasColumnName("player_name");
            entity.Property(e => e.LastMatchDate).HasColumnName("last_match_date");
        });

        // football_standings (V016 migration)
        modelBuilder.Entity<FootballStandingEntity>(entity =>
        {
            entity.ToTable("football_standings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CompetitionCode).HasColumnName("competition_code");
            entity.Property(e => e.CompetitionName).HasColumnName("competition_name");
            entity.Property(e => e.TeamExternalId).HasColumnName("team_external_id");
            entity.Property(e => e.GoalsFor).HasColumnName("goals_for");
            entity.Property(e => e.GoalsAgainst).HasColumnName("goals_against");
            entity.Property(e => e.GoalDifference).HasColumnName("goal_difference");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");
        });

        // tennis_rankings (V016 migration)
        modelBuilder.Entity<TennisRankingEntity>(entity =>
        {
            entity.ToTable("tennis_rankings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PlayerName).HasColumnName("player_name");
            entity.Property(e => e.PlayerKey).HasColumnName("player_key");
            entity.Property(e => e.FetchedAt).HasColumnName("fetched_at");
        });

        // historical_tennis_matches (V015 — tennis-data.co.uk)
        modelBuilder.Entity<HistoricalTennisMatchEntity>(entity =>
        {
            entity.ToTable("historical_tennis_matches");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MatchDate).HasColumnName("match_date");
            entity.Property(e => e.SeriesTier).HasColumnName("series_tier");
            entity.Property(e => e.WinnerNormalized).HasColumnName("winner_normalized");
            entity.Property(e => e.LoserNormalized).HasColumnName("loser_normalized");
            entity.Property(e => e.WinnerPts).HasColumnName("winner_pts");
            entity.Property(e => e.LoserPts).HasColumnName("loser_pts");
            entity.Property(e => e.Wsets).HasColumnName("wsets");
            entity.Property(e => e.Lsets).HasColumnName("lsets");
            entity.Property(e => e.SourceYear).HasColumnName("source_year");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}
