using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job for collecting live football statistics.
/// </summary>
public class FootballStatsJob : IJobService
{
    private readonly IFootballDataClient _footballClient;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<FootballStatsJob> _logger;

    /// <inheritdoc />
    public string CronExpression => "0 * * * *"; // Every hour

    /// <inheritdoc />
    public string JobId => "football-stats";

    public FootballStatsJob(
        IFootballDataClient footballClient,
        IMatchRepository matchRepository,
        ILogger<FootballStatsJob> logger)
    {
        _footballClient = footballClient;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executes the job to update live match statistics.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting football stats collection at {Time}", DateTime.UtcNow);

        var liveMatches = await _matchRepository.GetLiveAsync(cancellationToken);

        foreach (var match in liveMatches)
        {
            try
            {
                _logger.LogDebug("Fetching stats for match {MatchId}", match.ExternalId);

                var response = await _footballClient.GetMatchAsync(match.ExternalId);

                if (response.IsSuccessStatusCode && response.Content?.Match is not null)
                {
                    var matchDto = response.Content.Match;

                    match.Status = MapStatus(matchDto.Status);
                    match.HomeScore = matchDto.Score?.FullTime?.Home;
                    match.AwayScore = matchDto.Score?.FullTime?.Away;
                    match.UpdatedAt = DateTime.UtcNow;

                    await _matchRepository.UpsertAsync(match, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stats for match {MatchId}", match.ExternalId);
            }
        }

        _logger.LogInformation("Football stats collection completed. Updated {Count} matches", liveMatches.Count());
    }

    private static string MapStatus(string? status) => status?.ToUpper() switch
    {
        "SCHEDULED" or "TIMED" => "SCHEDULED",
        "LIVE" or "IN_PLAY" => "LIVE",
        "FINISHED" => "FINISHED",
        _ => "SCHEDULED"
    };
}
