using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job for collecting odds from the-odds-api.com.
/// </summary>
public class OddsCollectorJob : IJobService
{
    private readonly IOddsApiClient _oddsClient;
    private readonly IMatchRepository _matchRepository;
    private readonly IOddsRepository _oddsRepository;
    private readonly ILogger<OddsCollectorJob> _logger;
    private readonly string _apiKey;

    /// <inheritdoc />
    public string CronExpression => "*/30 * * * *"; // Every 30 minutes

    /// <inheritdoc />
    public string JobId => "odds-collector";

    public OddsCollectorJob(
        IOddsApiClient oddsClient,
        IMatchRepository matchRepository,
        IOddsRepository oddsRepository,
        ILogger<OddsCollectorJob> logger,
        IConfiguration configuration)
    {
        _oddsClient = oddsClient;
        _matchRepository = matchRepository;
        _oddsRepository = oddsRepository;
        _logger = logger;
        _apiKey = configuration["ApiKeys:Odds"] ?? "";
    }

    /// <summary>
    /// Executes the job to collect odds for upcoming matches.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting odds collection at {Time}, apiKey length={KeyLen}", DateTime.UtcNow, _apiKey.Length);

        var sports = new[] { "soccer_epl", "soccer_spain_la_liga", "soccer_germany_bundesliga",
                             "soccer_italy_serie_a", "soccer_france_ligue_one", "soccer_portugal_primeira_liga",
                             "tennis_atp", "tennis_wta" };

        foreach (var sport in sports)
        {
            try
            {
                _logger.LogInformation("Fetching odds for sport {Sport}", sport);

                var commenceFrom = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var commenceTo = DateTime.UtcNow.AddDays(14).ToString("yyyy-MM-ddTHH:mm:ssZ");

                var response = await _oddsClient.GetOddsAsync(
                    sport,
                    regions: "eu",
                    markets: "h2h,totals",
                    commenceTimeFrom: commenceFrom,
                    commenceTimeTo: commenceTo);

                if (response.IsSuccessStatusCode && response.Content is not null)
                {
                    var oddsList = new List<OddsEntity>();

                    foreach (var eventOdds in response.Content)
                    {
                        if (eventOdds.HomeTeam is null || eventOdds.AwayTeam is null || eventOdds.CommenceTime is null)
                            continue;

                        // Mapear evento da Odds API para o nosso match interno por nome de equipa + data
                        var matchEntity = await _matchRepository.FindByTeamNamesAndDateAsync(
                            eventOdds.HomeTeam,
                            eventOdds.AwayTeam,
                            eventOdds.CommenceTime.Value,
                            cancellationToken);

                        if (matchEntity is null)
                        {
                            _logger.LogInformation("No match found for {Home} vs {Away} on {Date}",
                                eventOdds.HomeTeam, eventOdds.AwayTeam, eventOdds.CommenceTime);
                            continue;
                        }

                        if (eventOdds.Bookmakers is not null)
                        {
                            foreach (var bookmaker in eventOdds.Bookmakers)
                            {
                                if (bookmaker.Markets is not null)
                                {
                                    foreach (var market in bookmaker.Markets)
                                    {
                                        if (market.Outcomes is not null)
                                        {
                                            foreach (var outcome in market.Outcomes)
                                            {
                                                if (outcome.Price.HasValue && outcome.Name is not null)
                                                {
                                                    // Normalize h2h outcomes: team names → "1"/"X"/"2"
                                                    var marketKey = market.Key ?? "h2h";
                                                    var outcomeName = outcome.Name;
                                                    if (marketKey == "h2h")
                                                    {
                                                        marketKey = "1X2";
                                                        outcomeName = NormalizeH2HOutcome(outcomeName, eventOdds.HomeTeam, eventOdds.AwayTeam);
                                                    }
                                                    // Normalize totals: "Over" + point 2.5 → "over 2.5"
                                                    else if (marketKey == "totals" && outcome.Point.HasValue)
                                                    {
                                                        outcomeName = $"{outcomeName.ToLower()} {outcome.Point:G}";
                                                    }

                                                    var odd = new OddsEntity
                                                    {
                                                        Id = Guid.NewGuid(),
                                                        MatchId = matchEntity.Id,
                                                        Bookmaker = bookmaker.Key ?? "unknown",
                                                        Market = marketKey,
                                                        Outcome = outcomeName,
                                                        OddDecimal = outcome.Price.Value,
                                                        ImpliedProbability = 1m / outcome.Price.Value,
                                                        CapturedAt = DateTime.UtcNow
                                                    };
                                                    oddsList.Add(odd);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (oddsList.Any())
                    {
                        await _oddsRepository.BulkInsertAsync(oddsList, cancellationToken);
                        _logger.LogInformation("Inserted {Count} odds for {Sport}", oddsList.Count, sport);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to fetch odds for {Sport}: {StatusCode}",
                        sport, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting odds for {Sport}", sport);
            }
        }

        _logger.LogInformation("Odds collection completed at {Time}", DateTime.UtcNow);
    }

    private static string NormalizeH2HOutcome(string outcome, string homeTeam, string awayTeam)
    {
        if (outcome.Contains("Draw", StringComparison.OrdinalIgnoreCase))
            return "X";

        // Check if outcome matches home team (fuzzy)
        if (homeTeam.Contains(outcome, StringComparison.OrdinalIgnoreCase) ||
            outcome.Contains(homeTeam, StringComparison.OrdinalIgnoreCase))
            return "1";

        // Check if outcome matches away team (fuzzy)
        if (awayTeam.Contains(outcome, StringComparison.OrdinalIgnoreCase) ||
            outcome.Contains(awayTeam, StringComparison.OrdinalIgnoreCase))
            return "2";

        return outcome; // fallback: keep as-is
    }
}
