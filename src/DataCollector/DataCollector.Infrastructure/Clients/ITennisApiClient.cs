using Refit;
using System.Text.Json.Serialization;

namespace SportsBetting.DataCollector.Infrastructure.Clients;

public interface ITennisApiClient
{
    [Get("/")]
    Task<ApiResponse<TennisApiResponse>> GetTournamentsAsync(
        [Query] string method = "get_tournaments",
        [Query] string APIkey = "");

    [Get("/")]
    Task<ApiResponse<TennisApiResponse>> GetHeadToHeadAsync(
        [Query] string method = "get_H2H",
        [Query] string? firstPlayer = null,
        [Query] string? secondPlayer = null,
        [Query] string APIkey = "");

    [Get("/")]
    Task<ApiResponse<TennisApiResponse>> GetLiveScoresAsync(
        [Query] string method = "get_live_score",
        [Query] string APIkey = "");

    [Get("/")]
    Task<ApiResponse<TennisFixtureResponse>> GetFixturesAsync(
        [Query] string method,
        [Query] string APIkey,
        [Query] string date_start,
        [Query] string date_stop,
        [Query] string? timezone = null);

    /// <summary>
    /// Gets current ATP or WTA rankings.
    /// tour: "atp" or "wta"
    /// </summary>
    [Get("/")]
    Task<ApiResponse<TennisRankingsResponse>> GetRankingsAsync(
        [Query] string method = "get_rankings",
        [Query] string tour = "atp",
        [Query] string APIkey = "");
}

public class TennisApiResponse
{
    public bool Success { get; set; }
    public List<TournamentDto>? Result { get; set; }
    public string? Error { get; set; }
}

public class TennisFixtureResponse
{
    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("result")]
    public List<FixtureDto>? Result { get; set; }
}

public class TournamentDto
{
    public string? TournamentName { get; set; }
    public string? Surface { get; set; }
    public List<MatchInfoDto>? Matches { get; set; }
}

public class MatchInfoDto
{
    public string? MatchId { get; set; }
    public string? HomePlayer { get; set; }
    public string? AwayPlayer { get; set; }
    public string? HomeSets { get; set; }
    public string? AwaySets { get; set; }
    public string? Status { get; set; }
}

public class TennisRankingsResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("result")]
    public List<TennisRankingEntryDto>? Result { get; set; }
}

public class TennisRankingEntryDto
{
    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("player_key")]
    public long? PlayerKey { get; set; }

    [JsonPropertyName("player_name")]
    public string? PlayerName { get; set; }

    [JsonPropertyName("player_country_key")]
    public string? PlayerCountryKey { get; set; }

    [JsonPropertyName("ranking_points")]
    public int? RankingPoints { get; set; }
}

public class FixtureDto
{
    [JsonPropertyName("event_key")]
    public long? EventKey { get; set; }

    [JsonPropertyName("event_date")]
    public string? EventDate { get; set; }

    [JsonPropertyName("event_time")]
    public string? EventTime { get; set; }

    [JsonPropertyName("event_first_player")]
    public string? EventFirstPlayer { get; set; }

    [JsonPropertyName("first_player_key")]
    public long? FirstPlayerKey { get; set; }

    [JsonPropertyName("event_second_player")]
    public string? EventSecondPlayer { get; set; }

    [JsonPropertyName("second_player_key")]
    public long? SecondPlayerKey { get; set; }

    [JsonPropertyName("event_final_result")]
    public string? EventFinalResult { get; set; }

    [JsonPropertyName("event_game_result")]
    public string? EventGameResult { get; set; }

    [JsonPropertyName("event_serve")]
    public string? EventServe { get; set; }

    [JsonPropertyName("event_winner")]
    public string? EventWinner { get; set; }

    [JsonPropertyName("event_status")]
    public string? EventStatus { get; set; }

    [JsonPropertyName("event_type_type")]
    public string? EventTypeType { get; set; }

    [JsonPropertyName("tournament_name")]
    public string? TournamentName { get; set; }

    [JsonPropertyName("tournament_key")]
    public long? TournamentKey { get; set; }

    [JsonPropertyName("tournament_round")]
    public string? TournamentRound { get; set; }

    [JsonPropertyName("tournament_season")]
    public string? TournamentSeason { get; set; }

    [JsonPropertyName("event_live")]
    public string? EventLive { get; set; }

    [JsonPropertyName("event_qualification")]
    public string? EventQualification { get; set; }
}
