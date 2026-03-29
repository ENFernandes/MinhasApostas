using SportsBetting.DataCollector.Core.Dtos;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SportsBetting.DataCollector.Infrastructure.Services;

/// <summary>
/// Service implementation for team statistics using external APIs.
/// </summary>
public class TeamStatsService : ITeamStatsService, IScopedService
{
    private readonly SportsBettingDbContext _context;
    private readonly IFootballDataClient _footballClient;
    private readonly ILogger<TeamStatsService> _logger;

    public TeamStatsService(
        SportsBettingDbContext context,
        IFootballDataClient footballClient,
        ILogger<TeamStatsService> logger)
    {
        _context = context;
        _footballClient = footballClient;
        _logger = logger;
    }

    public async Task<TeamFormDto> GetTeamFormAsync(string teamName, string sport, CancellationToken cancellationToken = default)
    {
        if (sport.Equals("football", StringComparison.OrdinalIgnoreCase))
        {
            return await GetFootballTeamFormAsync(teamName, cancellationToken);
        }

        return new TeamFormDto { TeamName = teamName };
    }

    public async Task<HeadToHeadDto> GetHeadToHeadAsync(string homeTeam, string awayTeam, string sport, CancellationToken cancellationToken = default)
    {
        if (sport.Equals("football", StringComparison.OrdinalIgnoreCase))
        {
            return await GetFootballHeadToHeadAsync(homeTeam, awayTeam, cancellationToken);
        }

        return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
    }

    private async Task<TeamFormDto> GetFootballTeamFormAsync(string teamName, CancellationToken cancellationToken)
    {
        var team = await _context.Matches
            .Where(m => (m.HomeTeam != null && m.HomeTeam.Name == teamName) || 
                        (m.AwayTeam != null && m.AwayTeam.Name == teamName))
            .Select(m => m.HomeTeam != null && m.HomeTeam.Name == teamName ? m.HomeTeam : m.AwayTeam)
            .FirstOrDefaultAsync(cancellationToken);

        if (team?.ExternalId == null)
        {
            _logger.LogWarning("Team not found in database: {TeamName}", teamName);
            return new TeamFormDto { TeamName = teamName };
        }

        try
        {
            var dateFrom = DateTime.UtcNow.AddDays(-90).ToString("yyyy-MM-dd");
            var dateTo = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var response = await _footballClient.GetTeamMatchesAsync(
                team.ExternalId,
                dateFrom,
                dateTo,
                cancellationToken: cancellationToken);

            if (response?.Content?.Matches == null || !response.Content.Matches.Any())
            {
                return new TeamFormDto { TeamName = teamName };
            }

            var matches = response.Content.Matches
                .Where(m => m.Status == "FINISHED" &&
                            (m.HomeTeam?.Name == teamName || m.AwayTeam?.Name == teamName))
                .OrderByDescending(m => m.UtcDate)
                .Take(10)
                .ToList();

            var results = new List<MatchResultDto>();
            int wins = 0, draws = 0, losses = 0;
            int goalsScored = 0, goalsConceded = 0;
            int homeGoalsScored = 0, homeGoalsConceded = 0;
            int awayGoalsScored = 0, awayGoalsConceded = 0;

            foreach (var match in matches)
            {
                var isHome = match.HomeTeam?.Name == teamName;
                var opponent = isHome ? match.AwayTeam?.Name : match.HomeTeam?.Name ?? "Unknown";
                var myGoals = isHome ? match.Score?.FullTime?.Home : match.Score?.FullTime?.Away;
                var oppGoals = isHome ? match.Score?.FullTime?.Away : match.Score?.FullTime?.Home;

                var result = "D";
                if (myGoals.HasValue && oppGoals.HasValue)
                {
                    if (myGoals > oppGoals) { result = "W"; wins++; }
                    else if (myGoals < oppGoals) { result = "L"; losses++; }
                    else { draws++; }

                    goalsScored += myGoals.Value;
                    goalsConceded += oppGoals.Value;

                    if (isHome)
                    {
                        homeGoalsScored += myGoals.Value;
                        homeGoalsConceded += oppGoals.Value;
                    }
                    else
                    {
                        awayGoalsScored += myGoals.Value;
                        awayGoalsConceded += oppGoals.Value;
                    }
                }

                results.Add(new MatchResultDto
                {
                    Date = match.UtcDate ?? DateTime.MinValue,
                    Opponent = opponent,
                    IsHome = isHome,
                    GoalsScored = myGoals ?? 0,
                    GoalsConceded = oppGoals ?? 0,
                    Result = result
                });
            }

            var homeMatches = results.Where(r => r.IsHome).ToList();
            var awayMatches = results.Where(r => !r.IsHome).ToList();

            return new TeamFormDto
            {
                TeamName = teamName,
                Last10Games = results,
                AvgGoalsScoredHome = homeMatches.Any() ? (float)homeMatches.Average(r => r.GoalsScored) : 0,
                AvgGoalsScoredAway = awayMatches.Any() ? (float)awayMatches.Average(r => r.GoalsScored) : 0,
                AvgGoalsConcededHome = homeMatches.Any() ? (float)homeMatches.Average(r => r.GoalsConceded) : 0,
                AvgGoalsConcededAway = awayMatches.Any() ? (float)awayMatches.Average(r => r.GoalsConceded) : 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching team form for {TeamName}", teamName);
            return new TeamFormDto { TeamName = teamName };
        }
    }

    private async Task<HeadToHeadDto> GetFootballHeadToHeadAsync(string homeTeam, string awayTeam, CancellationToken cancellationToken)
    {
        var homeTeamEntity = await _context.Matches
            .Where(m => m.HomeTeam != null && m.HomeTeam.Name == homeTeam)
            .Select(m => m.HomeTeam)
            .FirstOrDefaultAsync(cancellationToken);

        var awayTeamEntity = await _context.Matches
            .Where(m => m.AwayTeam != null && m.AwayTeam.Name == awayTeam)
            .Select(m => m.AwayTeam)
            .FirstOrDefaultAsync(cancellationToken);

        if (homeTeamEntity?.ExternalId == null || awayTeamEntity?.ExternalId == null)
        {
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }

        try
        {
            var dateFrom = DateTime.UtcNow.AddYears(-2).ToString("yyyy-MM-dd");
            var dateTo = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var homeResponse = await _footballClient.GetTeamMatchesAsync(
                homeTeamEntity.ExternalId, dateFrom, dateTo, cancellationToken: cancellationToken);

            if (homeResponse?.Content?.Matches == null)
            {
                return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
            }

            var h2hMatches = homeResponse.Content.Matches
                .Where(m => m.Status == "FINISHED" && m.HomeTeam?.Name != null && m.AwayTeam?.Name != null &&
                           ((m.HomeTeam.Name == homeTeam && m.AwayTeam.Name == awayTeam) ||
                            (m.HomeTeam.Name == awayTeam && m.AwayTeam.Name == homeTeam)))
                .OrderByDescending(m => m.UtcDate)
                .Take(10)
                .ToList();

            int homeWins = 0, awayWins = 0, draws = 0;
            int homeGoals = 0, awayGoals = 0;
            var matchResults = new List<MatchResultDto>();

            foreach (var match in h2hMatches)
            {
                var isHomePlaying = match.HomeTeam?.Name == homeTeam;
                var homeScore = match.Score?.FullTime?.Home ?? 0;
                var awayScore = match.Score?.FullTime?.Away ?? 0;

                string result;
                if (homeScore > awayScore)
                {
                    result = isHomePlaying ? "H" : "A";
                    if (isHomePlaying) homeWins++; else awayWins++;
                }
                else if (homeScore < awayScore)
                {
                    result = isHomePlaying ? "A" : "H";
                    if (isHomePlaying) awayWins++; else homeWins++;
                }
                else
                {
                    result = "D";
                    draws++;
                }

                homeGoals += homeScore;
                awayGoals += awayScore;

                matchResults.Add(new MatchResultDto
                {
                    Date = match.UtcDate ?? DateTime.MinValue,
                    Opponent = isHomePlaying ? awayTeam : homeTeam,
                    IsHome = isHomePlaying,
                    GoalsScored = isHomePlaying ? homeScore : awayScore,
                    GoalsConceded = isHomePlaying ? awayScore : homeScore,
                    Result = result
                });
            }

            return new HeadToHeadDto
            {
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                HomeTeamWins = homeWins,
                AwayTeamWins = awayWins,
                Draws = draws,
                TotalMatches = h2hMatches.Count,
                HomeTeamGoals = homeGoals,
                AwayTeamGoals = awayGoals,
                Matches = matchResults
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching head-to-head for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);
            return new HeadToHeadDto { HomeTeam = homeTeam, AwayTeam = awayTeam };
        }
    }
}
