using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job for collecting odds from the-odds-api.com.
/// </summary>
public class OddsCollectorJob : IJobService
{
    private readonly IOddsApiClient _oddsClient;
    private readonly IMatchRepository _matchRepository;
    private readonly ITeamRepository _teamRepository;
    private readonly IOddsRepository _oddsRepository;
    private readonly IMessageQueuePublisher _messageQueue;
    private readonly ILogger<OddsCollectorJob> _logger;
    private readonly string _apiKey;
    private readonly SportsBettingDbContext _dbContext;

    /// <inheritdoc />
    public string CronExpression => "*/30 * * * *"; // Every 30 minutes

    /// <inheritdoc />
    public string JobId => "odds-collector";

    public OddsCollectorJob(
        IOddsApiClient oddsClient,
        IMatchRepository matchRepository,
        ITeamRepository teamRepository,
        IOddsRepository oddsRepository,
        IMessageQueuePublisher messageQueue,
        ILogger<OddsCollectorJob> logger,
        IConfiguration configuration,
        SportsBettingDbContext dbContext)
    {
        _oddsClient = oddsClient;
        _matchRepository = matchRepository;
        _teamRepository = teamRepository;
        _oddsRepository = oddsRepository;
        _messageQueue = messageQueue;
        _logger = logger;
        _apiKey = configuration["ApiKeys:Odds"] ?? "";
        _dbContext = dbContext;
    }

    /// <summary>
    /// Executes the job to collect odds for upcoming matches.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting odds collection at {Time}, apiKey length={KeyLen}", DateTime.UtcNow, _apiKey.Length);

        var sports = new List<string>
        {
            "soccer_epl",
            "soccer_spain_la_liga",
            "soccer_germany_bundesliga",
            "soccer_italy_serie_a",
            "soccer_france_ligue_one",
            "soccer_portugal_primeira_liga",
        };

        // Tennis keys on the Odds API can vary by season/tournament. Discover active tennis keys dynamically.
        // We also keep a title map to create matches from odds events when needed.
        var sportTitleByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var sportsResponse = await _oddsClient.GetSportsAsync();
            if (sportsResponse.IsSuccessStatusCode && sportsResponse.Content is not null)
            {
                var tennisKeys = sportsResponse.Content
                    .Where(s => s.Active)
                    .Where(s => (s.Group ?? "").Contains("Tennis", StringComparison.OrdinalIgnoreCase)
                             || (s.Key ?? "").StartsWith("tennis_", StringComparison.OrdinalIgnoreCase))
                    .Select(s =>
                    {
                        var k = s.Key?.Trim();
                        if (!string.IsNullOrWhiteSpace(k))
                        {
                            sportTitleByKey[k] = s.Title?.Trim() ?? k;
                        }
                        return k;
                    })
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct()
                    .ToList();

                if (tennisKeys.Count > 0)
                {
                    _logger.LogInformation("Discovered {Count} active tennis sports keys", tennisKeys.Count);
                    sports.AddRange(tennisKeys!);
                }
                else
                {
                    _logger.LogWarning("No active tennis sports keys discovered from Odds API");
                }
            }
            else
            {
                _logger.LogWarning("Failed to fetch sports list from Odds API: {StatusCode}", sportsResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to discover tennis sport keys; tennis odds may be unavailable");
        }

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

                        // Tennis: if we don't have the match in DB, create it from the odds event
                        if (matchEntity is null && sport.StartsWith("tennis_", StringComparison.OrdinalIgnoreCase))
                        {
                            matchEntity = await CreateTennisMatchFromOddsEventAsync(
                                sport,
                                sportTitleByKey.TryGetValue(sport, out var title) ? title : sport,
                                eventOdds,
                                cancellationToken);
                        }

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
                                                        // Normalize against the internal match home/away to keep "1"/"2" consistent even if provider inverted teams.
                                                        var internalHome = matchEntity.HomeTeam?.Name ?? eventOdds.HomeTeam;
                                                        var internalAway = matchEntity.AwayTeam?.Name ?? eventOdds.AwayTeam;
                                                        outcomeName = NormalizeH2HOutcome(outcomeName, internalHome, internalAway);
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
                        // Replace odds for matches touched in this run to avoid unique constraint collisions.
                        var matchIdsForSport = oddsList.Select(o => o.MatchId).Distinct().ToList();
                        await _oddsRepository.ReplaceForMatchesAsync(matchIdsForSport, oddsList, cancellationToken);
                        _logger.LogInformation("Upserted {Count} odds for {Sport} across {MatchCount} matches",
                            oddsList.Count, sport, matchIdsForSport.Count);

                        // Publicar evento por jogo para o analysis engine re-analisar com as odds atualizadas
                        foreach (var matchId in matchIdsForSport)
                        {
                            try
                            {
                                await _messageQueue.PublishAsync(
                                    "sports.events",
                                    "odds.updated",
                                    new { match_id = matchId, updated_at = DateTime.UtcNow },
                                    cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to publish odds.updated for match {MatchId}", matchId);
                            }
                        }
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

    private async Task<MatchEntity?> CreateTennisMatchFromOddsEventAsync(
        string sportKey,
        string sportTitle,
        EventOddsDto eventOdds,
        CancellationToken cancellationToken)
    {
        try
        {
            var homeName = eventOdds.HomeTeam?.Trim();
            var awayName = eventOdds.AwayTeam?.Trim();
            if (string.IsNullOrWhiteSpace(homeName) || string.IsNullOrWhiteSpace(awayName) || eventOdds.CommenceTime is null)
                return null;

            var home = await GetOrCreateTennisPlayerAsync(homeName, cancellationToken);
            var away = await GetOrCreateTennisPlayerAsync(awayName, cancellationToken);
            var competition = await GetOrCreateTennisCompetitionAsync(sportKey, sportTitle, cancellationToken);

            var externalId = $"odds-{eventOdds.Id}";
            var match = new MatchEntity
            {
                ExternalId = externalId,
                Sport = "tennis",
                CommenceTime = DateTime.SpecifyKind(eventOdds.CommenceTime.Value, DateTimeKind.Utc),
                Status = "SCHEDULED",
                CompetitionId = competition?.Id,
                HomeId = home.Id,
                AwayId = away.Id,
            };

            await _matchRepository.UpsertAsync(match, cancellationToken);
            return await _matchRepository.GetByExternalIdAsync(externalId, "tennis", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create tennis match from odds event");
            return null;
        }
    }

    private async Task<TeamEntity> GetOrCreateTennisPlayerAsync(string playerName, CancellationToken cancellationToken)
    {
        var slug = Slugify(playerName);
        var externalId = $"tennis-player-{slug}";
        var existing = await _teamRepository.GetByExternalIdAsync(externalId, "tennis", cancellationToken);
        if (existing is not null)
            return existing;

        var team = new TeamEntity
        {
            ExternalId = externalId,
            Sport = "tennis",
            Name = playerName,
            ShortName = null,
            Country = null,
        };
        await _teamRepository.UpsertAsync(team, cancellationToken);
        return (await _teamRepository.GetByExternalIdAsync(externalId, "tennis", cancellationToken))!;
    }

    private async Task<CompetitionEntity?> GetOrCreateTennisCompetitionAsync(string sportKey, string title, CancellationToken cancellationToken)
    {
        var externalId = $"odds-{sportKey}";
        var existing = await _dbContext.Competitions.FirstOrDefaultAsync(
            c => c.ExternalId == externalId && c.Sport == "tennis",
            cancellationToken);

        if (existing is not null)
            return existing;

        var comp = new CompetitionEntity
        {
            ExternalId = externalId,
            Sport = "tennis",
            Name = title,
            Country = null,
            Season = DateTime.UtcNow.Year.ToString(),
        };
        _dbContext.Competitions.Add(comp);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return comp;
    }

    private static string Slugify(string input)
    {
        var normalized = input.Trim().ToLowerInvariant();
        var chars = normalized.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return slug.Trim('-');
    }
}
