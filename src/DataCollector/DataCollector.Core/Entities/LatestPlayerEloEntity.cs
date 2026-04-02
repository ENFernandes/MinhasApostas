namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>
/// Keyless entity mapped to the DB view latest_player_elo (latest ELO per player and surface).
/// </summary>
public class LatestPlayerEloEntity
{
    public string PlayerName { get; set; } = string.Empty;
    public string Surface { get; set; } = string.Empty;
    public double Elo { get; set; }
    public string? Tour { get; set; }
    public long? LastMatchDate { get; set; }
}

