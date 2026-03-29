using System.Text.Json.Serialization;
using Refit;

namespace SportsBetting.DataCollector.Infrastructure.Clients;

/// <summary>
/// Refit client for the-odds-api.com API.
/// </summary>
public interface IOddsApiClient
{
    /// <summary>
    /// Gets list of available sports.
    /// </summary>
    [Get("/sports")]
    Task<ApiResponse<List<SportDto>>> GetSportsAsync(
        [Query] bool all = false);

    /// <summary>
    /// Gets odds for a specific sport.
    /// </summary>
    [Get("/sports/{sport}/odds")]
    Task<ApiResponse<List<EventOddsDto>>> GetOddsAsync(
        string sport,
        [Query] string regions,
        [Query] string markets = "h2h",
        [Query] string? eventIds = null,
        [Query] string? commenceTimeFrom = null,
        [Query] string? commenceTimeTo = null);
}

public class SportDto
{
    public string? Key { get; set; }
    public string? Group { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; }
    public bool HasOutrights { get; set; }
}

public class EventOddsDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("sport_key")]
    public string? SportKey { get; set; }

    [JsonPropertyName("home_team")]
    public string? HomeTeam { get; set; }

    [JsonPropertyName("away_team")]
    public string? AwayTeam { get; set; }

    [JsonPropertyName("commence_time")]
    public DateTime? CommenceTime { get; set; }

    [JsonPropertyName("bookmakers")]
    public List<BookmakerDto>? Bookmakers { get; set; }
}

public class BookmakerDto
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("markets")]
    public List<MarketDto>? Markets { get; set; }
}

public class MarketDto
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }

    [JsonPropertyName("outcomes")]
    public List<OutcomeDto>? Outcomes { get; set; }
}

public class OutcomeDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("price")]
    public decimal? Price { get; set; }

    [JsonPropertyName("point")]
    public decimal? Point { get; set; }
}
