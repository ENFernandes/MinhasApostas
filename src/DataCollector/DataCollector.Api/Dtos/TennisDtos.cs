namespace SportsBetting.DataCollector.Api.Dtos;

/// <summary>
/// Tennis stats response for a player, based on seeded ELO + recent results.
/// </summary>
public class TennisPlayerStatsDto
{
    public string PlayerName { get; set; } = string.Empty;
    public Dictionary<string, double> EloBySurface { get; set; } = new();
    public List<TennisRecentResultDto> RecentResults { get; set; } = new();
}

/// <summary>
/// A recent tennis result (W/L) derived from Sackmann snapshots.
/// </summary>
public class TennisRecentResultDto
{
    public DateTime Date { get; set; }
    public string Surface { get; set; } = string.Empty;
    public string Tour { get; set; } = string.Empty;
    public bool Won { get; set; }
}

