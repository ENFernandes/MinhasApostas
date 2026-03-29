using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for bets.
/// </summary>
public class BetRepository : IBetRepository, IScopedService
{
    private readonly SportsBettingDbContext _context;

    public BetRepository(SportsBettingDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<BetEntity>> GetSettledBetsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Bets
            .Include(b => b.Match)
            .Include(b => b.Recommendation)
            .Where(b => b.Result == "WIN" || b.Result == "LOSS" || b.Result == "VOID")
            .OrderByDescending(b => b.SettledAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BetEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Bets
            .Include(b => b.Match)
            .Include(b => b.Recommendation)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BetEntity?> GetByRecommendationIdAsync(Guid recommendationId, CancellationToken cancellationToken = default)
    {
        return await _context.Bets
            .Include(b => b.Match)
            .Include(b => b.Recommendation)
            .FirstOrDefaultAsync(b => b.RecommendationId == recommendationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BetEntity> CreateAsync(BetEntity bet, CancellationToken cancellationToken = default)
    {
        bet.Id = Guid.NewGuid();
        bet.PlacedAt = DateTime.UtcNow;
        
        _context.Bets.Add(bet);
        await _context.SaveChangesAsync(cancellationToken);
        
        return bet;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(BetEntity bet, CancellationToken cancellationToken = default)
    {
        _context.Bets.Update(bet);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PerformanceStats> GetPerformanceStatsAsync(CancellationToken cancellationToken = default)
    {
        var settledBets = await _context.Bets
            .Include(b => b.Match)
            .Where(b => b.Result == "WIN" || b.Result == "LOSS")
            .ToListAsync(cancellationToken);

        if (settledBets.Count == 0)
        {
            return new PerformanceStats
            {
                TotalBets = 0,
                WinningBets = 0,
                LosingBets = 0,
                TotalProfitLoss = 0,
                WinRate = 0,
                Roi = 0,
                ByMarket = new List<PerformanceByMarket>(),
                BySport = new List<PerformanceBySport>()
            };
        }

        var totalProfitLoss = settledBets.Sum(b => b.ProfitLoss ?? 0);
        var totalStaked = settledBets.Sum(b => b.StakeEuros);
        var roi = totalStaked > 0 ? totalProfitLoss / totalStaked : 0;

        // Group by market
        var byMarket = settledBets
            .GroupBy(b => b.Market)
            .Select(g => new PerformanceByMarket
            {
                Market = g.Key,
                TotalBets = g.Count(),
                WinRate = g.Count() > 0 ? (decimal)g.Count(b => b.Result == "WIN") / g.Count() : 0,
                ProfitLoss = g.Sum(b => b.ProfitLoss ?? 0),
                Roi = g.Sum(b => b.StakeEuros) > 0 ? g.Sum(b => b.ProfitLoss ?? 0) / g.Sum(b => b.StakeEuros) : 0
            })
            .ToList();

        // Group by sport
        var bySport = settledBets
            .GroupBy(b => b.Match?.Sport ?? "unknown")
            .Select(g => new PerformanceBySport
            {
                Sport = g.Key,
                TotalBets = g.Count(),
                WinRate = g.Count() > 0 ? (decimal)g.Count(b => b.Result == "WIN") / g.Count() : 0,
                ProfitLoss = g.Sum(b => b.ProfitLoss ?? 0),
                Roi = g.Sum(b => b.StakeEuros) > 0 ? g.Sum(b => b.ProfitLoss ?? 0) / g.Sum(b => b.StakeEuros) : 0
            })
            .ToList();

        return new PerformanceStats
        {
            TotalBets = settledBets.Count,
            WinningBets = settledBets.Count(b => b.Result == "WIN"),
            LosingBets = settledBets.Count(b => b.Result == "LOSS"),
            TotalProfitLoss = totalProfitLoss,
            WinRate = (decimal)settledBets.Count(b => b.Result == "WIN") / settledBets.Count,
            Roi = roi,
            ByMarket = byMarket,
            BySport = bySport
        };
    }
}
