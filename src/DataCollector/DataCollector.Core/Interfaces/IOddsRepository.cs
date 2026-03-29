using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Repository interface for odds.
/// </summary>
public interface IOddsRepository : IRepository<OddsEntity>
{
    Task<IEnumerable<OddsEntity>> GetByMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OddsEntity>> GetLatestByMatchAsync(Guid matchId, CancellationToken cancellationToken = default);
    Task BulkInsertAsync(IEnumerable<OddsEntity> odds, CancellationToken cancellationToken = default);
}
