using SportsBetting.DataCollector.Core.Dtos;

namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Service interface for team statistics.
/// </summary>
public interface ITeamStatsService
{
    /// <summary>
    /// Gets recent form for a team by name.
    /// </summary>
    Task<TeamFormDto> GetTeamFormAsync(string teamName, string sport, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets head-to-head stats between two teams.
    /// </summary>
    Task<HeadToHeadDto> GetHeadToHeadAsync(string homeTeam, string awayTeam, string sport, CancellationToken cancellationToken = default);
}
