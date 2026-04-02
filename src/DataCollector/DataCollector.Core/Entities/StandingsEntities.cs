namespace SportsBetting.DataCollector.Core.Entities;

/// <summary>Classificação de uma liga de futebol (football_standings).</summary>
public class FootballStandingEntity
{
    public long Id { get; set; }
    public string CompetitionCode { get; set; } = string.Empty;
    public string? CompetitionName { get; set; }
    public string Season { get; set; } = string.Empty;
    public string Stage { get; set; } = "REGULAR_SEASON";
    public short Position { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int? TeamExternalId { get; set; }
    public short Played { get; set; }
    public short Won { get; set; }
    public short Draw { get; set; }
    public short Lost { get; set; }
    public short GoalsFor { get; set; }
    public short GoalsAgainst { get; set; }
    public short GoalDifference { get; set; }
    public short Points { get; set; }
    public string? Form { get; set; }
    public DateTime FetchedAt { get; set; }
}

/// <summary>Ranking ATP ou WTA (tennis_rankings).</summary>
public class TennisRankingEntity
{
    public long Id { get; set; }
    public string Tour { get; set; } = string.Empty;    // ATP / WTA
    public int Rank { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public long? PlayerKey { get; set; }
    public string? Country { get; set; }
    public int? Points { get; set; }
    public DateTime FetchedAt { get; set; }
}
