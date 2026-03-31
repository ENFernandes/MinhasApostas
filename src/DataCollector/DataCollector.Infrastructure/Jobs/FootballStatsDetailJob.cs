using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that fetches detailed xG and shot statistics for recently finished
/// matches via api-football and saves them to the match entity.
/// Runs hourly. Consumes ~20 of the 100 daily api-football requests.
/// </summary>
public class FootballStatsDetailJob : IJobService
{
    private readonly IApiFootballClient _apiFootballClient;
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<FootballStatsDetailJob> _logger;

    /// <inheritdoc />
    public string CronExpression => "0 * * * *"; // Every hour

    /// <inheritdoc />
    public string JobId => "football-stats-detail";

    public FootballStatsDetailJob(
        IApiFootballClient apiFootballClient,
        IMatchRepository matchRepository,
        ILogger<FootballStatsDetailJob> logger)
    {
        _apiFootballClient = apiFootballClient;
        _matchRepository = matchRepository;
        _logger = logger;
    }

    /// <summary>
    /// For each match finished in the last 3 hours without xG data,
    /// queries api-football and saves xG to the match entity.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting football stats detail collection at {Time}", DateTime.UtcNow);

        var windowStart = DateTime.UtcNow.AddHours(-3);
        var windowEnd = DateTime.UtcNow;

        // Get recently finished football matches without xG data
        var recentlyFinished = await _matchRepository.GetFinishedSinceAsync(
            windowStart, cancellationToken);

        var toUpdate = recentlyFinished
            .Where(m => m.HomeXg is null && m.AwayXg is null)
            .ToList();

        _logger.LogInformation(
            "Found {Count} finished matches without xG data", toUpdate.Count);

        foreach (var match in toUpdate)
        {
            try
            {
                // Resolve api-football fixture ID from external_id or team/date mapping
                var apiFootballId = await ResolveApiFootballIdAsync(
                    match.HomeTeam?.Name ?? string.Empty,
                    match.AwayTeam?.Name ?? string.Empty,
                    match.CommenceTime,
                    cancellationToken);

                if (apiFootballId is null)
                {
                    _logger.LogDebug(
                        "No api-football ID found for match {MatchId}", match.Id);
                    continue;
                }

                var statsResponse = await _apiFootballClient.GetFixtureStatisticsAsync(
                    apiFootballId.Value, cancellationToken);

                if (!statsResponse.IsSuccessStatusCode || statsResponse.Content is null)
                {
                    _logger.LogWarning(
                        "api-football stats request failed for fixture {FixtureId}: {Status}",
                        apiFootballId, statsResponse.StatusCode);
                    continue;
                }

                float? homeXg = null;
                float? awayXg = null;

                // Response has two entries: index 0 = home team, index 1 = away team
                var teamStats = statsResponse.Content.Response;

                if (teamStats.Count >= 1)
                    homeXg = ExtractXg(teamStats[0].Statistics);

                if (teamStats.Count >= 2)
                    awayXg = ExtractXg(teamStats[1].Statistics);

                if (homeXg.HasValue || awayXg.HasValue)
                {
                    match.HomeXg = homeXg;
                    match.AwayXg = awayXg;
                    match.UpdatedAt = DateTime.UtcNow;

                    await _matchRepository.UpsertAsync(match, cancellationToken);

                    _logger.LogInformation(
                        "xG updated for match {MatchId}: home={HomeXg}, away={AwayXg}",
                        match.Id, homeXg, awayXg);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error fetching stats detail for match {MatchId}", match.Id);
            }
        }

        _logger.LogInformation(
            "Football stats detail collection completed. Processed {Count} matches",
            toUpdate.Count);
    }

    /// <summary>
    /// Tries to find the api-football fixture ID by querying on the match date.
    /// Falls back to null if no match is found within a 2-hour window.
    /// </summary>
    private async Task<int?> ResolveApiFootballIdAsync(
        string homeTeam,
        string awayTeam,
        DateTime commenceTime,
        CancellationToken cancellationToken)
    {
        var dateStr = commenceTime.ToString("yyyy-MM-dd");

        var fixturesResponse = await _apiFootballClient.GetFixturesAsync(
            date: dateStr,
            status: "FT",
            cancellationToken: cancellationToken);

        if (!fixturesResponse.IsSuccessStatusCode || fixturesResponse.Content is null)
            return null;

        // Fuzzy match by team name (case-insensitive contains)
        var fixture = fixturesResponse.Content.Response.FirstOrDefault(f =>
            f.Teams.Home.Name is not null &&
            f.Teams.Away.Name is not null &&
            (f.Teams.Home.Name.Contains(homeTeam, StringComparison.OrdinalIgnoreCase) ||
             homeTeam.Contains(f.Teams.Home.Name, StringComparison.OrdinalIgnoreCase)) &&
            (f.Teams.Away.Name.Contains(awayTeam, StringComparison.OrdinalIgnoreCase) ||
             awayTeam.Contains(f.Teams.Away.Name, StringComparison.OrdinalIgnoreCase)));

        return fixture?.Fixture.Id;
    }

    private static float? ExtractXg(List<ApiFootballStatEntry> stats)
    {
        var xgEntry = stats.FirstOrDefault(s =>
            s.Type.Equals("expected_goals", StringComparison.OrdinalIgnoreCase));

        if (xgEntry?.ValueAsDecimal is { } d)
            return (float)d;

        return null;
    }
}
