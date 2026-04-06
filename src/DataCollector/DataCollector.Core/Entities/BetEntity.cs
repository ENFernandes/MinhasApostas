namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents a placed bet and its result.
/// </summary>
public class BetEntity
{
    public Guid Id { get; set; }
    public Guid? RecommendationId { get; set; }
    /// <summary>Null quando a aposta é registada só com texto livre (sem jogo na BD).</summary>
    public Guid? MatchId { get; set; }
    public string? ManualEventLabel { get; set; }
    public string? ManualSport { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? Bookmaker { get; set; }
    public decimal OddPlaced { get; set; }
    public decimal StakeEuros { get; set; }
    public string? Result { get; set; } // PENDING, WIN, LOSS, VOID
    public decimal? ProfitLoss { get; set; }
    public DateTime PlacedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }
    
    // Navigation properties
    public RecommendationEntity? Recommendation { get; set; }
    public MatchEntity? Match { get; set; }
}
