using Refit;

namespace SportsBetting.DataCollector.Infrastructure.Clients;

/// <summary>
/// Refit client for football-data.org API.
/// </summary>
public interface IFootballDataClient
{
    /// <summary>
    /// Gets matches for a specific competition.
    /// </summary>
    [Get("/competitions/{competitionId}/matches")]
    Task<ApiResponse<CompetitionMatchesResponse>> GetMatchesByCompetitionAsync(
        string competitionId,
        [Query] string? status = null,
        [Query] string? dateFrom = null,
        [Query] string? dateTo = null);

    /// <summary>
    /// Gets details for a specific match.
    /// </summary>
    [Get("/matches/{matchId}")]
    Task<ApiResponse<MatchResponse>> GetMatchAsync(string matchId);

    /// <summary>
    /// Gets details for a specific team.
    /// </summary>
    [Get("/teams/{teamId}")]
    Task<ApiResponse<TeamResponse>> GetTeamAsync(string teamId);

    /// <summary>
    /// Gets matches for a specific team.
    /// </summary>
    [Get("/teams/{teamId}/matches")]
    Task<ApiResponse<TeamMatchesResponse>> GetTeamMatchesAsync(
        string teamId,
        [Query] string? dateFrom = null,
        [Query] string? dateTo = null,
        [Query] string? venue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all competitions.
    /// </summary>
    [Get("/competitions")]
    Task<ApiResponse<CompetitionsResponse>> GetCompetitionsAsync();

    /// <summary>
    /// Gets the standings (tabela classificativa) for a competition.
    /// </summary>
    [Get("/competitions/{competitionId}/standings")]
    Task<ApiResponse<StandingsResponse>> GetStandingsAsync(string competitionId);
}

// Response models
public class CompetitionMatchesResponse
{
    public List<MatchDto> Matches { get; set; } = new();
}

public class MatchResponse
{
    public MatchDto Match { get; set; } = new();
}

public class TeamResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public AreaDto? Area { get; set; }
}

public class TeamMatchesResponse
{
    public List<MatchDto> Matches { get; set; } = new();
    public TeamDto? Team { get; set; }
}

public class CompetitionsResponse
{
    public List<CompetitionDto> Competitions { get; set; } = new();
}

public class MatchDto
{
    public int Id { get; set; }
    public string? Status { get; set; }
    public DateTime? UtcDate { get; set; }
    public CompetitionDto? Competition { get; set; }
    public TeamDto? HomeTeam { get; set; }
    public TeamDto? AwayTeam { get; set; }
    public ScoreDto? Score { get; set; }
}

public class CompetitionDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public AreaDto? Area { get; set; }
}

public class TeamDto
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
}

public class ScoreDto
{
    public string? Winner { get; set; }
    public ScoreDetailDto? FullTime { get; set; }
    public ScoreDetailDto? HalfTime { get; set; }
}

public class ScoreDetailDto
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}

public class AreaDto
{
    public string? Name { get; set; }
    public string? Code { get; set; }
}

// ── Standings models ──────────────────────────────────────────────────────────

public class StandingsResponse
{
    public CompetitionDto? Competition { get; set; }
    public SeasonDto? Season { get; set; }
    public List<StandingsTableDto> Standings { get; set; } = new();
}

public class SeasonDto
{
    public int? Id { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? CurrentMatchday { get; set; }
}

public class StandingsTableDto
{
    public string? Stage { get; set; }
    public string? Type { get; set; }     // TOTAL / HOME / AWAY
    public List<StandingEntryDto> Table { get; set; } = new();
}

public class StandingEntryDto
{
    public int Position { get; set; }
    public TeamDto? Team { get; set; }
    public int PlayedGames { get; set; }
    public string? Form { get; set; }
    public int Won { get; set; }
    public int Draw { get; set; }
    public int Lost { get; set; }
    public int Points { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
}
