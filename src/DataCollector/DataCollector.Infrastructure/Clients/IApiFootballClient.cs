using Refit;

namespace SportsBetting.DataCollector.Infrastructure.Clients;

/// <summary>
/// Refit client for api-football (v3.football.api-sports.io).
/// Rate limit: 100 req/day on free plan — use sparingly.
/// </summary>
public interface IApiFootballClient
{
    /// <summary>
    /// Gets detailed statistics for a fixture (xG, shots, possession, etc.).
    /// </summary>
    [Get("/fixtures/statistics")]
    Task<ApiResponse<ApiFootballStatisticsResponse>> GetFixtureStatisticsAsync(
        [Query] int fixture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets confirmed lineups for a fixture.
    /// </summary>
    [Get("/fixtures/lineups")]
    Task<ApiResponse<ApiFootballLineupsResponse>> GetFixtureLineupsAsync(
        [Query] int fixture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets injuries for a team or league.
    /// </summary>
    [Get("/injuries")]
    Task<ApiResponse<ApiFootballInjuriesResponse>> GetInjuriesAsync(
        [Query] int? league = null,
        [Query] int? season = null,
        [Query] int? team = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for a fixture by date and teams (used for ID mapping).
    /// </summary>
    [Get("/fixtures")]
    Task<ApiResponse<ApiFootballFixturesResponse>> GetFixturesAsync(
        [Query] string? date = null,
        [Query] int? league = null,
        [Query] int? season = null,
        [Query] int? team = null,
        [Query] string? status = null,
        CancellationToken cancellationToken = default);
}

// ── Response models ──────────────────────────────────────────────────────────

public class ApiFootballStatisticsResponse
{
    public List<ApiFootballTeamStatistics> Response { get; set; } = new();
}

public class ApiFootballTeamStatistics
{
    public ApiFootballTeamDto Team { get; set; } = new();
    public List<ApiFootballStatEntry> Statistics { get; set; } = new();
}

public class ApiFootballStatEntry
{
    public string Type { get; set; } = string.Empty;
    public object? Value { get; set; }

    /// <summary>Returns Value as decimal, or null if not parseable.</summary>
    public decimal? ValueAsDecimal =>
        Value is null ? null
        : decimal.TryParse(Value.ToString(), out var d) ? d : null;

    /// <summary>Returns Value as string (e.g. "56%" for possession).</summary>
    public string? ValueAsString => Value?.ToString();
}

public class ApiFootballLineupsResponse
{
    public List<ApiFootballTeamLineup> Response { get; set; } = new();
}

public class ApiFootballTeamLineup
{
    public ApiFootballTeamDto Team { get; set; } = new();
    public string? Formation { get; set; }
    public List<ApiFootballPlayerEntry> StartXI { get; set; } = new();
    public List<ApiFootballPlayerEntry> Substitutes { get; set; } = new();
}

public class ApiFootballPlayerEntry
{
    public ApiFootballPlayerInfo Player { get; set; } = new();
}

public class ApiFootballPlayerInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? Number { get; set; }
    public string? Pos { get; set; }
}

public class ApiFootballInjuriesResponse
{
    public List<ApiFootballInjuryEntry> Response { get; set; } = new();
}

public class ApiFootballInjuryEntry
{
    public ApiFootballPlayerInfo Player { get; set; } = new();
    public ApiFootballTeamDto Team { get; set; } = new();
    public string? Type { get; set; }
    public string? Reason { get; set; }
}

public class ApiFootballFixturesResponse
{
    public List<ApiFootballFixtureEntry> Response { get; set; } = new();
}

public class ApiFootballFixtureEntry
{
    public ApiFootballFixtureInfo Fixture { get; set; } = new();
    public ApiFootballTeamsDto Teams { get; set; } = new();
}

public class ApiFootballFixtureInfo
{
    public int Id { get; set; }
    public string? Status { get; set; }
    public DateTime? Date { get; set; }
}

public class ApiFootballTeamsDto
{
    public ApiFootballTeamDto Home { get; set; } = new();
    public ApiFootballTeamDto Away { get; set; } = new();
}

public class ApiFootballTeamDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
}
