namespace SportsBetting.DataCollector.Api.Dtos;

/// <summary>
/// Tennis stats response for a player: ELO por superfície + resultados recentes + stats de serviço.
/// </summary>
public class TennisPlayerStatsDto
{
    public string PlayerName { get; set; } = string.Empty;
    public Dictionary<string, double> EloBySurface { get; set; } = new();
    public List<TennisRecentResultDto> RecentResults { get; set; } = new();
    public TennisServeStatsDto? ServeStats { get; set; }
}

/// <summary>
/// Resultado recente de ténis (W/L) com oponente, superfície e torneio.
/// </summary>
public class TennisRecentResultDto
{
    public DateTime Date { get; set; }
    public string Surface { get; set; } = string.Empty;
    public string Tour { get; set; } = string.Empty;
    public bool Won { get; set; }
    public string? Opponent { get; set; }
    public string? Score { get; set; }
    public string? TourneyName { get; set; }
    public string? Round { get; set; }
    public int? WinnerRank { get; set; }
    public int? LoserRank { get; set; }
}

/// <summary>
/// Estatísticas médias de serviço por superfície (últimos N jogos históricos).
/// </summary>
public class TennisServeStatsDto
{
    public Dictionary<string, TennisServeSurfaceDto> BySurface { get; set; } = new();
}

public class TennisServeSurfaceDto
{
    public int Matches { get; set; }
    public double AvgAces { get; set; }
    public double AvgDoubleFaults { get; set; }
    public double Pct1stServeIn { get; set; }
    public double Pct1stServeWon { get; set; }
    public double Pct2ndServeWon { get; set; }
    public double PctBreakPointsSaved { get; set; }
}

/// <summary>
/// H2H histórico entre dois jogadores de ténis.
/// </summary>
public class TennisH2HDto
{
    public string PlayerA { get; set; } = string.Empty;
    public string PlayerB { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public int PlayerAWins { get; set; }
    public int PlayerBWins { get; set; }
    public List<TennisH2HMatchDto> Matches { get; set; } = new();
}

public class TennisH2HMatchDto
{
    public DateTime? Date { get; set; }
    public string? TourneyName { get; set; }
    public string? Surface { get; set; }
    public string? Round { get; set; }
    public string Winner { get; set; } = string.Empty;
    public string Loser { get; set; } = string.Empty;
    public string? Score { get; set; }
    public int? WinnerRank { get; set; }
    public int? LoserRank { get; set; }
}
