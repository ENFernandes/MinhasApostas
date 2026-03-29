namespace SportsBetting.DataCollector.Core.Dtos;

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
