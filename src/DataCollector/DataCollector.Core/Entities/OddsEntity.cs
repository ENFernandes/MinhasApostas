using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents betting odds for a match.
/// </summary>
public class OddsEntity
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public string Bookmaker { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty; // h2h, totals, spreads
    public string Outcome { get; set; } = string.Empty; // home, draw, away, over_2.5, etc.
    public decimal OddDecimal { get; set; }
    public decimal ImpliedProbability { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
