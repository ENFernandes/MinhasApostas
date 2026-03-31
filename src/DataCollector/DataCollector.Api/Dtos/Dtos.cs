namespace SportsBetting.DataCollector.Api.Dtos;

/// <summary>
/// Data transfer object for a match.
/// </summary>
public class MatchDto
{
    public Guid Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
    public string? CompetitionName { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public DateTime CommenceTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public int? Minute { get; set; }
}

/// <summary>
/// Request parameters for filtering upcoming matches.
/// </summary>
public class GetUpcomingMatchesRequest
{
    public string? Sport { get; set; }
    public string? Competition { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool? OnlyWithOdds { get; set; }
}

/// <summary>
/// Data transfer object for match details with odds and stats.
/// </summary>
public class MatchDetailDto : MatchDto
{
    public List<OddsDto> LatestOdds { get; set; } = new();
    public MatchStatsDto? Stats { get; set; }
}

/// <summary>
/// Data transfer object for odds.
/// </summary>
public class OddsDto
{
    public string Bookmaker { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public decimal OddDecimal { get; set; }
    public decimal ImpliedProbability { get; set; }
    public DateTime CapturedAt { get; set; }
}

/// <summary>
/// Data transfer object for match statistics.
/// </summary>
public class MatchStatsDto
{
    public int? Shots { get; set; }
    public int? ShotsOnTarget { get; set; }
    public int? Possession { get; set; }
    public int? Corners { get; set; }
    public int? Fouls { get; set; }
    public int? YellowCards { get; set; }
    public int? RedCards { get; set; }
    public float? Xg { get; set; }
}

/// <summary>
/// Data transfer object for team form.
/// </summary>
public class TeamFormDto
{
    public string TeamName { get; set; } = string.Empty;
    public List<MatchResultDto> Last10Games { get; set; } = new();
    public float AvgGoalsScoredHome { get; set; }
    public float AvgGoalsScoredAway { get; set; }
    public float AvgGoalsConcededHome { get; set; }
    public float AvgGoalsConcededAway { get; set; }
}

/// <summary>
/// Data transfer object for a match result.
/// </summary>
public class MatchResultDto
{
    public DateTime Date { get; set; }
    public string Opponent { get; set; } = string.Empty;
    public bool IsHome { get; set; }
    public int GoalsScored { get; set; }
    public int GoalsConceded { get; set; }
    public string Result { get; set; } = string.Empty; // W, D, L
}

/// <summary>
/// Data transfer object for player statistics (tennis).
/// </summary>
public class PlayerStatsDto
{
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public int? Ranking { get; set; }
    public float? EloOverall { get; set; }
    public float? EloClay { get; set; }
    public float? EloGrass { get; set; }
    public float? EloHard { get; set; }
    public float? EloIndoor { get; set; }
}

/// <summary>
/// Data transfer object for a recommendation.
/// </summary>
public class RecommendationDto
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Bookmaker { get; set; } = string.Empty;
    public decimal OddDecimal { get; set; }
    public decimal ModelProbability { get; set; }
    public decimal ImpliedProbability { get; set; }
    public decimal Value { get; set; }
    public decimal KellyFraction { get; set; }
    public decimal StakeEuros { get; set; }
    public int Confidence { get; set; }
    public string? Reasoning { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to create a new recommendation.
/// </summary>
public class CreateRecommendationRequest
{
    public Guid MatchId { get; set; }
    public string Market { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Bookmaker { get; set; } = string.Empty;
    public decimal OddDecimal { get; set; }
    public decimal ModelProbability { get; set; }
    public decimal ImpliedProbability { get; set; }
    public decimal Value { get; set; }
    public decimal KellyFraction { get; set; }
    public decimal StakeEuros { get; set; }
    public int Confidence { get; set; }
    public string? Reasoning { get; set; }
    public string LlmProvider { get; set; } = string.Empty;
    public string LlmModel { get; set; } = string.Empty;
}

/// <summary>
/// Request to register a bet result.
/// </summary>
public class RegisterBetRequest
{
    public Guid RecommendationId { get; set; }
    public Guid? MatchId { get; set; }
    public string? Market { get; set; }
    public string? Bookmaker { get; set; }
    public decimal StakeActual { get; set; }
    public decimal OddActual { get; set; }
    public string Outcome { get; set; } = string.Empty; // WIN, LOSS, VOID
    public decimal ProfitLoss { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Data transfer object for performance statistics.
/// </summary>
public class PerformanceStatsDto
{
    public decimal TotalProfitLoss { get; set; }
    public decimal Roi { get; set; }
    public decimal WinRate { get; set; }
    public int TotalBets { get; set; }
    public int WinningBets { get; set; }
    public int LosingBets { get; set; }
    public List<PerformanceByMarketDto> ByMarket { get; set; } = new();
    public List<PerformanceBySportDto> BySport { get; set; } = new();
}

/// <summary>
/// Performance data grouped by market.
/// </summary>
public class PerformanceByMarketDto
{
    public string Market { get; set; } = string.Empty;
    public int TotalBets { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal Roi { get; set; }
}

/// <summary>
/// Performance data grouped by sport.
/// </summary>
public class PerformanceBySportDto
{
    public string Sport { get; set; } = string.Empty;
    public int TotalBets { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitLoss { get; set; }
    public decimal Roi { get; set; }
}

/// <summary>
/// Request parameters for filtering recommendations.
/// </summary>
public class GetRecommendationsRequest
{
    public DateTime? Date { get; set; }
    public string? Sport { get; set; }
    public string? Status { get; set; }
}

/// <summary>
/// Data transfer object for a bet result (settled bet).
/// </summary>
public class BetResultDto
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public MatchDto Match { get; set; } = null!;
    public RecommendedMarketDto Recommendation { get; set; } = null!;
    public decimal StakeActual { get; set; }
    public decimal OddActual { get; set; }
    public string Outcome { get; set; } = string.Empty; // WIN, LOSS, VOID
    public decimal ProfitLoss { get; set; }
    public DateTime SettledAt { get; set; }
}

/// <summary>
/// Recommended market information for bet result.
/// </summary>
public class RecommendedMarketDto
{
    public string Market { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string Bookmaker { get; set; } = string.Empty;
    public decimal Odd { get; set; }
    public decimal ImpliedProbability { get; set; }
    public decimal ModelProbability { get; set; }
    public decimal Value { get; set; }
    public decimal KellyFraction { get; set; }
    public decimal StakeEuros { get; set; }
    public int Confidence { get; set; }
}

/// <summary>
/// Data transfer object for head-to-head stats.
/// </summary>
public class HeadToHeadDto
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomeTeamWins { get; set; }
    public int AwayTeamWins { get; set; }
    public int Draws { get; set; }
    public int TotalMatches { get; set; }
    public int HomeTeamGoals { get; set; }
    public int AwayTeamGoals { get; set; }
    public List<MatchResultDto> Matches { get; set; } = new();
}
