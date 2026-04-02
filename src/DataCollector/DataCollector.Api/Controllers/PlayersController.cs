using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;
using SportsBetting.DataCollector.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for player data (tennis).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly ILogger<PlayersController> _logger;
    private readonly SportsBettingDbContext _context;

    public PlayersController(
        ILogger<PlayersController> logger,
        SportsBettingDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Gets statistics for a tennis player including ELO ratings.
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    public async Task<ActionResult<PlayerStatsDto>> GetStats(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Placeholder - would fetch from repository
        return Ok(new PlayerStatsDto
        {
            Name = "Player Name",
            Ranking = 10,
            EloOverall = 1800,
            EloClay = 1850,
            EloGrass = 1750,
            EloHard = 1820,
            EloIndoor = 1780
        });
    }

    /// <summary>
    /// Gets tennis stats by player name (seeded ELO + recent W/L results).
    /// </summary>
    [HttpGet("tennis/stats")]
    public async Task<ActionResult<TennisPlayerStatsDto>> GetTennisStats(
        [FromQuery] string playerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return BadRequest("playerName is required");

        var dto = new TennisPlayerStatsDto { PlayerName = playerName };

        // ELO by surface: use latest_player_elo view (may be empty if name not found).
        var trimmed = playerName.Trim();
        var pattern = $"%{trimmed}%";
        var latest = await _context.LatestPlayerElo
            .FromSqlInterpolated($"""
                SELECT player_name, surface, elo, tour, last_match_date
                FROM latest_player_elo
                WHERE player_name ILIKE {pattern}
                """)
            .ToListAsync(cancellationToken);

        // Abbreviation fallback: "J. Mikulskyte" -> (initial + last name).
        if (latest.Count == 0)
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts[0].EndsWith(".", StringComparison.Ordinal))
            {
                var initial = parts[0].TrimEnd('.');
                var lastName = parts[^1];
                var abbrPattern = $"{initial}% {lastName}";
                latest = await _context.LatestPlayerElo
                    .FromSqlInterpolated($"""
                        SELECT player_name, surface, elo, tour, last_match_date
                        FROM latest_player_elo
                        WHERE
                            player_name ILIKE {abbrPattern}
                        """)
                    .ToListAsync(cancellationToken);
            }
        }

        foreach (var row in latest)
        {
            var surface = string.IsNullOrWhiteSpace(row.Surface) ? "Unknown" : row.Surface;
            var elo = row.Elo;
            // Prefer higher ELO if multiple rows match different names.
            if (!dto.EloBySurface.TryGetValue(surface, out var existing) || elo > existing)
                dto.EloBySurface[surface] = elo;
        }

        // Recent results (W/L): last 15 snapshots, then de-dup by date/surface/won.
        var hist = await _context.PlayerEloHistory
            .FromSqlInterpolated($"""
                SELECT player_name, surface, elo, tour, match_date, won
                FROM player_elo_history
                WHERE player_name ILIKE {pattern}
                ORDER BY match_date DESC NULLS LAST
                LIMIT 15
                """)
            .ToListAsync(cancellationToken);

        if (hist.Count == 0)
        {
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2 && parts[0].EndsWith(".", StringComparison.Ordinal))
            {
                var initial = parts[0].TrimEnd('.');
                var lastName = parts[^1];
                var abbrPattern = $"{initial}% {lastName}";
                hist = await _context.PlayerEloHistory
                    .FromSqlInterpolated($"""
                        SELECT player_name, surface, elo, tour, match_date, won
                        FROM player_elo_history
                        WHERE
                            player_name ILIKE {abbrPattern}
                        ORDER BY match_date DESC NULLS LAST
                        LIMIT 15
                        """)
                    .ToListAsync(cancellationToken);
            }
        }

        dto.RecentResults = hist
            .Where(r => r.MatchDate.HasValue && r.Won.HasValue)
            .Select(r => new TennisRecentResultDto
            {
                Date = DateTime.TryParseExact(
                    r.MatchDate.Value.ToString(),
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsed)
                    ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                    : DateTime.UtcNow,
                Surface = r.Surface ?? "Unknown",
                Tour = r.Tour ?? "",
                Won = r.Won.Value
            })
            .GroupBy(r => new { r.Date, r.Surface, r.Won })
            .Select(g => g.First())
            .Take(10)
            .ToList();

        return Ok(dto);
    }
}
