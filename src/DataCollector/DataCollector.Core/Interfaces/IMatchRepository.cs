using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Repository interface for matches.
/// </summary>
public interface IMatchRepository : IRepository<MatchEntity>
{
    Task<MatchEntity?> GetByExternalIdAsync(string externalId, string sport, CancellationToken cancellationToken = default);
    Task<IEnumerable<MatchEntity>> GetUpcomingAsync(DateTime from, DateTime to, string? sport = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<MatchEntity>> GetLiveAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(MatchEntity match, CancellationToken cancellationToken = default);
    Task<MatchEntity?> FindByTeamNamesAndDateAsync(string homeTeam, string awayTeam, DateTime commenceTime, CancellationToken cancellationToken = default);
}
