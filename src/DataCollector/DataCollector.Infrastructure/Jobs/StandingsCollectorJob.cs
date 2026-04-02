using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job que recolhe de 2 em 2 dias:
///   1. Tabelas classificativas das principais ligas de futebol (football-data.org)
///   2. Rankings ATP e WTA (api-tennis.com)
///
/// Os dados ficam disponíveis via latest_football_standings e latest_tennis_rankings
/// para que a LLM possa contextualizar análises com posições e pontos actuais.
/// </summary>
public class StandingsCollectorJob : IJobService
{
    // Cron: às 06:00 todos os dias com número par (0, 2, 4, ...)
    // "0 6 */2 * *" = à 1:00 de 2 em 2 dias
    public string CronExpression => "0 6 */2 * *";
    public string JobId => "standings-collector";

    private static readonly string[] FootballCompetitions =
        ["PL", "PD", "BL1", "SA", "FL1", "PPL", "CL", "DED", "BSA"];

    private readonly IFootballDataClient _footballClient;
    private readonly ITennisApiClient _tennisClient;
    private readonly SportsBettingDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<StandingsCollectorJob> _logger;

    public StandingsCollectorJob(
        IFootballDataClient footballClient,
        ITennisApiClient tennisClient,
        SportsBettingDbContext db,
        IConfiguration config,
        ILogger<StandingsCollectorJob> logger)
    {
        _footballClient = footballClient;
        _tennisClient = tennisClient;
        _db = db;
        _config = config;
        _logger = logger;
    }

    /// <summary>Executes standings and rankings collection.</summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StandingsCollectorJob started at {Time}", DateTime.UtcNow);

        await CollectFootballStandingsAsync(cancellationToken);
        await CollectTennisRankingsAsync(cancellationToken);

        _logger.LogInformation("StandingsCollectorJob completed at {Time}", DateTime.UtcNow);
    }

    // ── Football standings ────────────────────────────────────────────────────

    private async Task CollectFootballStandingsAsync(CancellationToken cancellationToken)
    {
        int total = 0;
        var fetchedAt = DateTime.UtcNow;

        foreach (var competitionCode in FootballCompetitions)
        {
            try
            {
                var response = await _footballClient.GetStandingsAsync(competitionCode);

                if (!response.IsSuccessStatusCode || response.Content is null)
                {
                    _logger.LogWarning("Failed to fetch standings for {Competition}: {Status}",
                        competitionCode, response.StatusCode);
                    continue;
                }

                var content = response.Content;
                var competitionName = content.Competition?.Name;
                var season = content.Season is not null
                    ? $"{content.Season.StartDate?[..4]}/{content.Season.EndDate?[2..4]}"
                    : "unknown";

                // Usar apenas TOTAL standings (não HOME/AWAY separados)
                var totalTable = content.Standings
                    .FirstOrDefault(s => s.Type == "TOTAL")
                    ?? content.Standings.FirstOrDefault();

                if (totalTable is null) continue;

                var entities = totalTable.Table.Select(e => new FootballStandingEntity
                {
                    CompetitionCode = competitionCode,
                    CompetitionName = competitionName,
                    Season = season,
                    Stage = totalTable.Stage ?? "REGULAR_SEASON",
                    Position = (short)e.Position,
                    TeamName = e.Team?.Name ?? "Unknown",
                    TeamExternalId = e.Team?.Id,
                    Played = (short)e.PlayedGames,
                    Won = (short)e.Won,
                    Draw = (short)e.Draw,
                    Lost = (short)e.Lost,
                    GoalsFor = (short)e.GoalsFor,
                    GoalsAgainst = (short)e.GoalsAgainst,
                    GoalDifference = (short)e.GoalDifference,
                    Points = (short)e.Points,
                    Form = e.Form,
                    FetchedAt = fetchedAt,
                }).ToList();

                await _db.FootballStandings.AddRangeAsync(entities, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                total += entities.Count;
                _logger.LogInformation("Standings saved for {Competition}: {Count} entries",
                    competitionCode, entities.Count);

                // Respeitar rate limit (10 req/min no plano free)
                await Task.Delay(7_000, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting standings for {Competition}", competitionCode);
            }
        }

        _logger.LogInformation("Football standings collected: {Total} entries", total);
    }

    // ── Tennis rankings ───────────────────────────────────────────────────────

    private async Task CollectTennisRankingsAsync(CancellationToken cancellationToken)
    {
        var tennisKey = _config["TENNIS_API_KEY"] ?? string.Empty;
        int total = 0;
        var fetchedAt = DateTime.UtcNow;

        foreach (var tour in new[] { "atp", "wta" })
        {
            try
            {
                var response = await _tennisClient.GetRankingsAsync(
                    method: "get_rankings",
                    tour: tour,
                    APIkey: tennisKey);

                if (!response.IsSuccessStatusCode || response.Content?.Result is null)
                {
                    _logger.LogWarning("Failed to fetch {Tour} rankings: {Status}",
                        tour.ToUpper(), response.StatusCode);
                    continue;
                }

                var entities = response.Content.Result
                    .Where(r => !string.IsNullOrWhiteSpace(r.PlayerName))
                    .Select(r => new TennisRankingEntity
                    {
                        Tour = tour.ToUpper(),
                        Rank = r.Rank,
                        PlayerName = r.PlayerName!,
                        PlayerKey = r.PlayerKey,
                        Country = r.PlayerCountryKey,
                        Points = r.RankingPoints,
                        FetchedAt = fetchedAt,
                    }).ToList();

                await _db.TennisRankings.AddRangeAsync(entities, cancellationToken);
                await _db.SaveChangesAsync(cancellationToken);

                total += entities.Count;
                _logger.LogInformation("{Tour} rankings saved: {Count} entries",
                    tour.ToUpper(), entities.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting {Tour} rankings", tour.ToUpper());
            }
        }

        _logger.LogInformation("Tennis rankings collected: {Total} entries", total);
    }
}
