using SportsBetting.DataCollector.Core.Entities;

namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Repository interface for bets.
/// </summary>
public interface IBetRepository
{
    /// <summary>
    /// Gets all settled bets (WIN, LOSS, VOID) for performance tracking.
    /// </summary>
    Task<IEnumerable<BetEntity>> GetSettledBetsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a bet by ID.
    /// </summary>
    Task<BetEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a bet by recommendation ID.
    /// </summary>
    Task<BetEntity?> GetByRecommendationIdAsync(Guid recommendationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new bet.
    /// </summary>
    Task<BetEntity> CreateAsync(BetEntity bet, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a bet (e.g., when settling).
    /// </summary>
    Task UpdateAsync(BetEntity bet, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets performance statistics aggregated from all settled bets.
    /// </summary>
    Task<PerformanceStats> GetPerformanceStatsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Performance statistics DTO returned by the repository.
/// </summary>
public class PerformanceStats
{
    public int TotalBets { get; set; }
    public int WinningBets { get; set; }
    public int LosingBets { get; set; }
    public decimal TotalProfitLoss { get; set; }
    public decimal WinRate { get; set; }
    public decimal Roi { get; set; }
    public List<PerformanceByMarket> ByMarket { get; set; } = new();
    public List<PerformanceBySport> BySport { get; set; } = new();
}

public class PerformanceByMarket
{
    public string Market { get; set; } = string.Empty;
    public int TotalBets { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal Roi { get; set; }
}

public class PerformanceBySport
{
    public string Sport { get; set; } = string.Empty;
    public int TotalBets { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal Roi { get; set; }
}
