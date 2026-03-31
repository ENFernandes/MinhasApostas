using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Clients;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Jobs;

/// <summary>
/// Hangfire job that collects confirmed lineups for upcoming matches via api-football.
/// Runs at 20h D-1 and at 12h and 17h D-0.
/// Consumes ~10 of the 100 daily api-football requests.
/// Saves lineups to the match_lineups table.
/// </summary>
public class FootballLineupsJob : IJobService
{
    private readonly IApiFootballClient _apiFootballClient;
    private readonly IMatchRepository _matchRepository;
    private readonly SportsBettingDbContext _context;
    private readonly ILogger<FootballLineupsJob> _logger;

    /// <inheritdoc />
    public string CronExpression => "0 12,17,20 * * *"; // 12h, 17h, 20h daily

    /// <inheritdoc />
    public string JobId => "football-lineups";

    public FootballLineupsJob(
        IApiFootballClient apiFootballClient,
        IMatchRepository matchRepository,
        SportsBettingDbContext context,
        ILogger<FootballLineupsJob> logger)
    {
        _apiFootballClient = apiFootballClient;
        _matchRepository = matchRepository;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Fetches lineups for football matches in the next 24 hours
    /// that do not yet have confirmed lineups stored.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting football lineups collection at {Time}", DateTime.UtcNow);

        var upcoming = await _matchRepository.GetUpcomingAsync(
            from: DateTime.UtcNow,
            to: DateTime.UtcNow.AddHours(24),
            sport: "football",
            cancellationToken: cancellationToken);

        // Only process matches without lineups yet
        var matchIds = upcoming.Select(m => m.Id).ToList();
        var existingLineupMatchIds = await _context.Set<MatchLineupEntity>()
            .Where(l => matchIds.Contains(l.MatchId))
            .Select(l => l.MatchId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var withoutLineups = upcoming
            .Where(m => !existingLineupMatchIds.Contains(m.Id))
            .ToList();

        _logger.LogInformation(
            "Found {Count} upcoming matches without lineups", withoutLineups.Count);

        foreach (var match in withoutLineups)
        {
            try
            {
                var apiFootballId = await ResolveApiFootballIdAsync(
                    match.HomeTeam?.Name ?? string.Empty,
                    match.AwayTeam?.Name ?? string.Empty,
                    match.CommenceTime,
                    cancellationToken);

                if (apiFootballId is null)
                {
                    _logger.LogDebug(
                        "No api-football ID found for match {MatchId}", match.Id);
                    continue;
                }

                var lineupsResponse = await _apiFootballClient.GetFixtureLineupsAsync(
                    apiFootballId.Value, cancellationToken);

                if (!lineupsResponse.IsSuccessStatusCode || lineupsResponse.Content is null)
                {
                    _logger.LogDebug(
                        "Lineups not yet available for fixture {FixtureId}", apiFootballId);
                    continue;
                }

                var lineups = lineupsResponse.Content.Response;
                if (lineups.Count == 0)
                {
                    _logger.LogDebug(
                        "Empty lineups response for fixture {FixtureId}", apiFootballId);
                    continue;
                }

                foreach (var teamLineup in lineups)
                {
                    var isHome = teamLineup.Team.Name is not null &&
                        (match.HomeTeam?.Name?.Contains(
                            teamLineup.Team.Name, StringComparison.OrdinalIgnoreCase) == true ||
                         teamLineup.Team.Name.Contains(
                            match.HomeTeam?.Name ?? string.Empty,
                            StringComparison.OrdinalIgnoreCase));

                    var teamId = isHome ? match.HomeId : match.AwayId;

                    var players = teamLineup.StartXI.Select(p => new
                    {
                        name = p.Player.Name,
                        position = p.Player.Pos,
                        number = p.Player.Number,
                    }).ToList();

                    var substitutes = teamLineup.Substitutes.Select(p => new
                    {
                        name = p.Player.Name,
                        position = p.Player.Pos,
                        number = p.Player.Number,
                    }).ToList();

                    var entity = new MatchLineupEntity
                    {
                        Id = Guid.NewGuid(),
                        MatchId = match.Id,
                        TeamId = teamId,
                        Formation = teamLineup.Formation,
                        Players = JsonSerializer.Serialize(players),
                        Substitutes = JsonSerializer.Serialize(substitutes),
                        ConfirmedAt = DateTime.UtcNow,
                    };

                    _context.Set<MatchLineupEntity>().Add(entity);
                }

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Lineups saved for match {MatchId} (fixture {FixtureId})",
                    match.Id, apiFootballId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error fetching lineups for match {MatchId}", match.Id);
            }
        }

        _logger.LogInformation("Football lineups collection completed");
    }

    private async Task<int?> ResolveApiFootballIdAsync(
        string homeTeam,
        string awayTeam,
        DateTime commenceTime,
        CancellationToken cancellationToken)
    {
        var dateStr = commenceTime.ToString("yyyy-MM-dd");

        var fixturesResponse = await _apiFootballClient.GetFixturesAsync(
            date: dateStr,
            cancellationToken: cancellationToken);

        if (!fixturesResponse.IsSuccessStatusCode || fixturesResponse.Content is null)
            return null;

        var fixture = fixturesResponse.Content.Response.FirstOrDefault(f =>
            f.Teams.Home.Name is not null &&
            f.Teams.Away.Name is not null &&
            (f.Teams.Home.Name.Contains(homeTeam, StringComparison.OrdinalIgnoreCase) ||
             homeTeam.Contains(f.Teams.Home.Name, StringComparison.OrdinalIgnoreCase)) &&
            (f.Teams.Away.Name.Contains(awayTeam, StringComparison.OrdinalIgnoreCase) ||
             awayTeam.Contains(f.Teams.Away.Name, StringComparison.OrdinalIgnoreCase)));

        return fixture?.Fixture.Id;
    }
}

/// <summary>
/// Entity for match_lineups table (V011 migration).
/// </summary>
public class MatchLineupEntity
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Guid TeamId { get; set; }
    public string? Formation { get; set; }
    public string Players { get; set; } = "[]";       // JSONB — array of {name, position, number}
    public string Substitutes { get; set; } = "[]";   // JSONB
    public DateTime ConfirmedAt { get; set; } = DateTime.UtcNow;
}
