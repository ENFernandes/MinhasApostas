using SportsBetting.DataCollector.Core.Entities;

namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Repository interface for teams.
/// </summary>
public interface ITeamRepository
{
    /// <summary>
    /// Gets a team by external ID and sport.
    /// </summary>
    Task<TeamEntity?> GetByExternalIdAsync(string externalId, string sport, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a team.
    /// </summary>
    Task UpsertAsync(TeamEntity team, CancellationToken cancellationToken = default);
}
