using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for odds.
/// </summary>
public class OddsRepository : IOddsRepository
{
    private readonly SportsBettingDbContext _context;

    public OddsRepository(SportsBettingDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OddsEntity>> GetByMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        return await _context.Odds
            .Where(o => o.MatchId == matchId)
            .OrderByDescending(o => o.CapturedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<OddsEntity>> GetLatestByMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        var latestTimestamp = await _context.Odds
            .Where(o => o.MatchId == matchId)
            .MaxAsync(o => (DateTime?)o.CapturedAt, cancellationToken);

        if (latestTimestamp is null)
            return Enumerable.Empty<OddsEntity>();

        return await _context.Odds
            .Where(o => o.MatchId == matchId && o.CapturedAt == latestTimestamp)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task BulkInsertAsync(IEnumerable<OddsEntity> odds, CancellationToken cancellationToken = default)
    {
        await _context.Odds.AddRangeAsync(odds, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
