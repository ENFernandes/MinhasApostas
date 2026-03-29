using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;
using CompetitionEntity = SportsBetting.DataCollector.Core.Entities.CompetitionEntity;
using CompetitionDto = SportsBetting.DataCollector.Infrastructure.Clients.CompetitionDto;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job for collecting football match data from football-data.org.
/// </summary>
public class FootballCollectorJob : IJobService
{
    private readonly IFootballDataClient _footballClient;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IMessageQueuePublisher _messageQueue;
    private readonly ILogger<FootballCollectorJob> _logger;
    private readonly SportsBettingDbContext _dbContext;

    /// <inheritdoc />
    public string CronExpression => "0 */2 * * *"; // Every 2 hours

    /// <inheritdoc />
    public string JobId => "football-collector";

    public FootballCollectorJob(
        IFootballDataClient footballClient,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        IMessageQueuePublisher messageQueue,
        ILogger<FootballCollectorJob> logger,
        SportsBettingDbContext dbContext)
    {
        _footballClient = footballClient;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _messageQueue = messageQueue;
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes the job to collect upcoming football matches.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting football data collection at {Time}", DateTime.UtcNow);

        var competitions = new[] { "PL", "PD", "BL1", "SA", "FL1", "PPL", "CL" };
        int totalMatches = 0;

        foreach (var competition in competitions)
        {
            try
            {
                _logger.LogInformation("Fetching matches for competition {Competition}", competition);

                var dateFrom = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd"); // inclui ontem para atualizar statuses
                var dateTo = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-dd");

                var response = await _footballClient.GetMatchesByCompetitionAsync(
                    competition,
                    dateFrom: dateFrom,
                    dateTo: dateTo);

                if (response.IsSuccessStatusCode && response.Content is not null)
                {
                    foreach (var matchDto in response.Content.Matches)
                    {
                        await ProcessMatchAsync(matchDto, cancellationToken);
                        totalMatches++;
                    }

                    _logger.LogInformation("Processed {Count} matches for {Competition}",
                        response.Content.Matches.Count, competition);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch matches for {Competition}: {StatusCode}",
                        competition, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing competition {Competition}", competition);
            }
        }

        _logger.LogInformation("Football data collection completed. Total matches: {Total}", totalMatches);
    }

    private async Task ProcessMatchAsync(MatchDto matchDto, CancellationToken cancellationToken)
    {
        if (!matchDto.UtcDate.HasValue || matchDto.HomeTeam?.Id is null || matchDto.AwayTeam?.Id is null)
        {
            _logger.LogWarning("Skipping match with missing required data");
            return;
        }

        try
        {
            // Create or update teams first
            var homeTeam = await GetOrCreateTeamAsync(matchDto.HomeTeam, cancellationToken);
            var awayTeam = await GetOrCreateTeamAsync(matchDto.AwayTeam, cancellationToken);

            // Get or create competition
            var competition = await GetOrCreateCompetitionAsync(matchDto.Competition, cancellationToken);

            // Create match entity
            var match = new MatchEntity
            {
                ExternalId = matchDto.Id.ToString(),
                Sport = "football",
                CommenceTime = DateTime.SpecifyKind(matchDto.UtcDate.Value, DateTimeKind.Utc),
                Status = MapStatus(matchDto.Status),
                HomeScore = matchDto.Score?.FullTime?.Home,
                AwayScore = matchDto.Score?.FullTime?.Away,
                HomeId = homeTeam.Id,
                AwayId = awayTeam.Id,
                CompetitionId = competition?.Id,
            };

            // Save to database
            await _matchRepository.UpsertAsync(match, cancellationToken);

            // Publish message to queue
            var message = new
            {
                Id = match.Id,
                ExternalId = match.ExternalId,
                Sport = match.Sport,
                HomeId = match.HomeId,
                AwayId = match.AwayId,
                HomeTeam = homeTeam.Name,
                AwayTeam = awayTeam.Name,
                CommenceTime = match.CommenceTime,
                Status = match.Status,
                Competition = matchDto.Competition?.Name ?? "Unknown",
                CollectedAt = DateTime.UtcNow
            };

            await _messageQueue.PublishAsync(
                "sports.events",
                "football.match.new",
                message,
                cancellationToken);

            _logger.LogDebug("Published match {MatchId} to queue", match.ExternalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing match {MatchId}", matchDto.Id);
        }
    }

    private async Task<TeamEntity> GetOrCreateTeamAsync(TeamDto teamDto, CancellationToken cancellationToken)
    {
        var externalId = $"football-{teamDto.Id}";
        var team = await _teamRepository.GetByExternalIdAsync(externalId, "football", cancellationToken);

        if (team == null)
        {
            team = new TeamEntity
            {
                ExternalId = externalId,
                Sport = "football",
                Name = teamDto.Name ?? "Unknown",
                ShortName = teamDto.ShortName,
                Country = null, // Will be populated later
            };

            await _teamRepository.UpsertAsync(team, cancellationToken);
            _logger.LogDebug("Created team: {TeamName}", team.Name);
        }

        return team;
    }

    private async Task<CompetitionEntity?> GetOrCreateCompetitionAsync(CompetitionDto? compDto, CancellationToken cancellationToken)
    {
        if (compDto == null)
            return null;

        var externalId = $"football-{compDto.Id}";
        var competition = await Task.Run(() => 
            _dbContext.Competitions.FirstOrDefault(c => c.ExternalId == externalId), cancellationToken);

        if (competition == null)
        {
            competition = new CompetitionEntity
            {
                ExternalId = externalId,
                Sport = "football",
                Name = compDto.Name ?? "Unknown",
                Country = null,
            };

            _dbContext.Competitions.Add(competition);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Created competition: {CompetitionName}", competition.Name);
        }

        return competition;
    }

    private static string MapStatus(string? status) => status?.ToUpper() switch
    {
        "SCHEDULED" or "TIMED" => "SCHEDULED",
        "LIVE" or "IN_PLAY" => "LIVE",
        "FINISHED" => "FINISHED",
        _ => "SCHEDULED"
    };
}
