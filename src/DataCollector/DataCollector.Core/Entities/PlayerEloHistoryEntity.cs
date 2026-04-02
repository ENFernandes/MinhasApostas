namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Represents a historical ELO snapshot for a tennis player (seeded from Jeff Sackmann datasets).
/// </summary>
public class PlayerEloHistoryEntity
{
    public string PlayerName { get; set; } = string.Empty;
    public string Surface { get; set; } = string.Empty;
    public double Elo { get; set; }
    public string? Tour { get; set; }
    public long? MatchDate { get; set; } // yyyyMMdd (Sackmann) stored as bigint
    public bool? Won { get; set; }
}

