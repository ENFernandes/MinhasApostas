using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;
using SportsBetting.DataCollector.Infrastructure.Services;
using TeamEntity = SportsBetting.DataCollector.Core.Entities.TeamEntity;
using CompetitionEntity = SportsBetting.DataCollector.Core.Entities.CompetitionEntity;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

public class TennisCollectorJob : IJobService
{
    private readonly ITennisApiClient _tennisClient;
    private readonly IMatchRepository _matchRepository;
    private readonly IMessageQueuePublisher _messageQueue;
    private readonly ILogger<TennisCollectorJob> _logger;
    private readonly string _apiKey;
    private readonly SportsBettingDbContext _dbContext;

    public string CronExpression => "0 */2 * * *"; // Every 2 hours

    public string JobId => "tennis-collector";

    public TennisCollectorJob(
        ITennisApiClient tennisClient,
        IMatchRepository matchRepository,
        IMessageQueuePublisher messageQueue,
        ILogger<TennisCollectorJob> logger,
        IConfiguration configuration,
        SportsBettingDbContext dbContext)
    {
        _tennisClient = tennisClient;
        _matchRepository = matchRepository;
        _messageQueue = messageQueue;
        _logger = logger;
        _apiKey = configuration["ApiKeys:Tennis"] ?? throw new InvalidOperationException("Tennis API key not configured");
        _dbContext = dbContext;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting tennis data collection at {Time}", DateTime.UtcNow);

        var today = DateTime.UtcNow.Date;
        var dateStart = today.ToString("yyyy-MM-dd");
        var dateStop = today.AddDays(7).ToString("yyyy-MM-dd");

        _logger.LogInformation("Fetching tennis fixtures from {DateStart} to {DateStop}", dateStart, dateStop);

        try
        {
            var response = await _tennisClient.GetFixturesAsync(
                method: "get_fixtures",
                APIkey: _apiKey,
                date_start: dateStart,
                date_stop: dateStop);

            if (response.IsSuccessStatusCode && response.Content is not null)
            {
                if (response.Content.Success == 1 && response.Content.Result is not null)
                {
                    int totalMatches = 0;

                    foreach (var fixture in response.Content.Result)
                    {
                        try
                        {
                            _logger.LogDebug("Processing fixture: {EventKey} - {Home} vs {Away} - Status: {Status}",
                                fixture.EventKey, fixture.EventFirstPlayer, fixture.EventSecondPlayer, fixture.EventStatus);

                            // Skip finished matches
                            if (fixture.EventStatus?.ToUpper() == "FINISHED")
                            {
                                _logger.LogDebug("Skipping finished match {EventKey}", fixture.EventKey);
                                continue;
                            }

                            // Skip live matches (status "LIVE" or "IN PLAY")
                            if (fixture.EventStatus?.ToUpper() == "LIVE" || fixture.EventStatus?.ToUpper() == "IN PLAY")
                            {
                                _logger.LogDebug("Skipping live match {EventKey}", fixture.EventKey);
                                continue;
                            }

                            await ProcessFixtureAsync(fixture, cancellationToken);
                            totalMatches++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing fixture {EventKey}", fixture.EventKey);
                        }
                    }

                    _logger.LogInformation("Tennis data collection completed. Total matches: {Total}", totalMatches);
                }
                else
                {
                    _logger.LogWarning("Tennis API returned error or no data");
                }
            }
            else
            {
                _logger.LogWarning("Failed to fetch tennis data: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during tennis data collection");
        }
    }

    private async Task ProcessFixtureAsync(FixtureDto fixture, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking fixture: EventKey={EventKey}, FirstPlayer={FirstPlayer}, SecondPlayer={SecondPlayer}, Status={Status}",
            fixture.EventKey,
            fixture.EventFirstPlayer,
            fixture.EventSecondPlayer,
            fixture.EventStatus);

        if (!fixture.EventKey.HasValue)
        {
            _logger.LogWarning("Skipping tennis fixture: EventKey is null/empty. Data: {Data}", fixture);
            return;
        }

        if (string.IsNullOrEmpty(fixture.EventFirstPlayer))
        {
            _logger.LogWarning("Skipping tennis fixture {EventKey}: FirstPlayer is null/empty", fixture.EventKey);
            return;
        }

        if (string.IsNullOrEmpty(fixture.EventSecondPlayer))
        {
            _logger.LogWarning("Skipping tennis fixture {EventKey}: SecondPlayer is null/empty", fixture.EventKey);
            return;
        }

        try
        {
            DateTime commenceTime;
            if (DateTime.TryParse(fixture.EventDate, out var eventDate))
            {
                if (TimeSpan.TryParse(fixture.EventTime, out var eventTime))
                {
                    commenceTime = DateTime.SpecifyKind(eventDate.Date.Add(eventTime), DateTimeKind.Utc);
                }
                else
                {
                    commenceTime = DateTime.SpecifyKind(eventDate, DateTimeKind.Utc);
                }
            }
            else
            {
                commenceTime = DateTime.UtcNow.AddDays(1);
            }

            var competition = await GetOrCreateCompetitionAsync(fixture, cancellationToken);
            var homePlayer = await GetOrCreatePlayerAsync(fixture.EventFirstPlayer, fixture.FirstPlayerKey, cancellationToken);
            var awayPlayer = await GetOrCreatePlayerAsync(fixture.EventSecondPlayer, fixture.SecondPlayerKey, cancellationToken);

            var match = new MatchEntity
            {
                ExternalId = $"tennis-{fixture.EventKey}",
                Sport = "tennis",
                CommenceTime = commenceTime,
                Status = MapStatus(fixture.EventStatus),
                CompetitionId = competition?.Id,
                HomeId = homePlayer.Id,
                AwayId = awayPlayer.Id,
            };

            _logger.LogInformation("About to upsert tennis match: ExternalId={ExternalId}, HomeId={HomeId}, AwayId={AwayId}",
                $"tennis-{fixture.EventKey}", homePlayer.Id, awayPlayer.Id);

            await _matchRepository.UpsertAsync(match, cancellationToken);

            try
            {
                await _messageQueue.PublishAsync(
                    "sports.events",
                    "tennis.match.new",
                    new
                    {
                        ExternalId = match.ExternalId,
                        Sport = "tennis",
                        HomeTeam = homePlayer.Name,
                        AwayTeam = awayPlayer.Name,
                        CommenceTime = match.CommenceTime,
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish tennis.match.new for {ExternalId}", match.ExternalId);
            }

            _logger.LogInformation("Successfully processed tennis match: {Home} vs {Away} at {Time} - {Competition}",
                fixture.EventFirstPlayer, fixture.EventSecondPlayer, commenceTime, fixture.TournamentName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tennis fixture {EventKey}", fixture.EventKey);
        }
    }

    private async Task<TeamEntity> GetOrCreatePlayerAsync(string playerName, long? playerKey, CancellationToken cancellationToken)
    {
        var keyStr = playerKey.HasValue ? playerKey.Value.ToString() : playerName.Replace(" ", "-").ToLower();
        var externalId = $"tennis-player-{keyStr}";
        var player = await Task.Run(() => 
            _dbContext.Teams.FirstOrDefault(t => t.ExternalId == externalId), cancellationToken);

        if (player == null)
        {
            player = new TeamEntity
            {
                ExternalId = externalId,
                Sport = "tennis",
                Name = playerName,
                NameNormalized = TennisNameNormalizer.Normalize(playerName),
                ShortName = null,
                Country = null,
            };

            _dbContext.Teams.Add(player);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return player;
    }

    private async Task<CompetitionEntity?> GetOrCreateCompetitionAsync(FixtureDto fixture, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(fixture.TournamentName))
            return null;

        var keyStr = fixture.TournamentKey.HasValue ? fixture.TournamentKey.Value.ToString() : fixture.TournamentName;
        var externalId = $"tennis-{keyStr}";
        var competition = await Task.Run(() => 
            _dbContext.Competitions.FirstOrDefault(c => c.ExternalId == externalId), cancellationToken);

        if (competition == null)
        {
            competition = new CompetitionEntity
            {
                ExternalId = externalId,
                Sport = "tennis",
                Name = fixture.TournamentName,
                Country = null,
                Season = fixture.TournamentSeason,
            };

            _dbContext.Competitions.Add(competition);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Created tennis competition: {CompetitionName}", competition.Name);
        }

        return competition;
    }

    private static string MapStatus(string? status) => status?.ToUpper() switch
    {
        "NOT STARTED" => "SCHEDULED",
        "1" => "SCHEDULED", // Upcoming (not started yet)
        "LIVE" or "IN PLAY" => "LIVE",
        "FINISHED" => "FINISHED",
        _ => "SCHEDULED"
    };
}
