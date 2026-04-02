namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Jogo histórico de ténis (ATP/WTA) carregado de tennis-data.co.uk (.xlsx).
/// Mapeado para <c>historical_tennis_matches</c> (V015).
/// </summary>
public class HistoricalTennisMatchEntity
{
    public long Id { get; set; }

    public string? Location { get; set; }
    public string? Tournament { get; set; }
    public DateTime? MatchDate { get; set; }
    public string? SeriesTier { get; set; }
    public string? Court { get; set; }
    public string? Surface { get; set; }

    public short? MatchNum { get; set; }
    public string? Round { get; set; }
    public short? BestOf { get; set; }

    public string Winner { get; set; } = string.Empty;
    public string? WinnerNormalized { get; set; }
    public string Loser { get; set; } = string.Empty;
    public string? LoserNormalized { get; set; }
    public int? WinnerRank { get; set; }
    public int? LoserRank { get; set; }
    public int? WinnerPts { get; set; }
    public int? LoserPts { get; set; }

    public short? W1 { get; set; }
    public short? L1 { get; set; }
    public short? W2 { get; set; }
    public short? L2 { get; set; }
    public short? W3 { get; set; }
    public short? L3 { get; set; }
    public short? W4 { get; set; }
    public short? L4 { get; set; }
    public short? W5 { get; set; }
    public short? L5 { get; set; }
    public short? Wsets { get; set; }
    public short? Lsets { get; set; }
    public string? Comment { get; set; }

    public string Tour { get; set; } = string.Empty;
    public short? SourceYear { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAt { get; set; }
}
