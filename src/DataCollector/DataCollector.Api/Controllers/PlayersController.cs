using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;
using SportsBetting.DataCollector.Infrastructure.Data;
using SportsBetting.DataCollector.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

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
    /// Gets tennis stats by player name: ELO por superfície, resultados recentes
    /// e médias de serviço histórico (aces, break points, etc.).
    /// </summary>
    [HttpGet("tennis/stats")]
    public async Task<ActionResult<TennisPlayerStatsDto>> GetTennisStats(
        [FromQuery] string playerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return BadRequest("playerName is required");

        var dto = new TennisPlayerStatsDto { PlayerName = playerName };
        var trimmed = playerName.Trim();
        var normalized = TennisNameNormalizer.Normalize(trimmed);
        var fuzzyPattern = $"%{TennisNameNormalizer.SurnameToken(normalized)}%";

        // ── ELO por superfície ────────────────────────────────────────────────
        var latest = await _context.LatestPlayerElo
            .FromSqlInterpolated($"""
                SELECT player_name, surface, elo, tour, last_match_date
                FROM latest_player_elo
                WHERE player_name ILIKE {fuzzyPattern}
                """)
            .ToListAsync(cancellationToken);

        foreach (var row in latest)
        {
            var surface = string.IsNullOrWhiteSpace(row.Surface) ? "Unknown" : row.Surface;
            if (!dto.EloBySurface.TryGetValue(surface, out var existing) || row.Elo > existing)
                dto.EloBySurface[surface] = row.Elo;
        }

        // ── Resultados recentes (tennis-data.co.uk em historical_tennis_matches) ──
        var histMatches = await _context.HistoricalTennisMatches
            .Where(m =>
                m.WinnerNormalized == normalized || m.LoserNormalized == normalized ||
                EF.Functions.ILike(m.Winner, fuzzyPattern) ||
                EF.Functions.ILike(m.Loser, fuzzyPattern))
            .OrderByDescending(m => m.MatchDate)
            .Take(10)
            .ToListAsync(cancellationToken);

        dto.RecentResults = histMatches.Select(m =>
        {
            var won = m.WinnerNormalized == normalized ||
                      m.Winner.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
            return new TennisRecentResultDto
            {
                Date = MatchDateToUtc(m.MatchDate),
                Surface = m.Surface ?? "Unknown",
                Tour = m.Tour ?? "",
                Won = won,
                Opponent = won ? m.Loser : m.Winner,
                Score = FormatSetScore(m),
                TourneyName = m.Tournament,
                Round = m.Round,
                WinnerRank = m.WinnerRank,
                LoserRank = m.LoserRank,
            };
        }).ToList();

        // tennis-data.co.uk não inclui stats de serviço por jogo — resposta vazia até haver fonte
        dto.ServeStats = new TennisServeStatsDto { BySurface = new() };

        return Ok(dto);
    }

    /// <summary>
    /// Gets head-to-head history between two tennis players (historical_tennis_matches, tennis-data.co.uk).
    /// </summary>
    [HttpGet("tennis/h2h")]
    public async Task<ActionResult<TennisH2HDto>> GetTennisH2H(
        [FromQuery] string playerA,
        [FromQuery] string playerB,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerA) || string.IsNullOrWhiteSpace(playerB))
            return BadRequest("playerA and playerB are required");

        var normA = TennisNameNormalizer.Normalize(playerA.Trim());
        var normB = TennisNameNormalizer.Normalize(playerB.Trim());
        var fuzzyA = $"%{TennisNameNormalizer.SurnameToken(normA)}%";
        var fuzzyB = $"%{TennisNameNormalizer.SurnameToken(normB)}%";

        var matches = await _context.HistoricalTennisMatches
            .Where(m =>
                ((m.WinnerNormalized == normA || EF.Functions.ILike(m.Winner, fuzzyA)) &&
                 (m.LoserNormalized  == normB || EF.Functions.ILike(m.Loser,  fuzzyB))) ||
                ((m.WinnerNormalized == normB || EF.Functions.ILike(m.Winner, fuzzyB)) &&
                 (m.LoserNormalized  == normA || EF.Functions.ILike(m.Loser,  fuzzyA))))
            .OrderByDescending(m => m.MatchDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        int aWins = matches.Count(m => m.WinnerNormalized == normA ||
            m.Winner.Contains(playerA.Trim(), StringComparison.OrdinalIgnoreCase));
        int bWins = matches.Count(m => m.WinnerNormalized == normB ||
            m.Winner.Contains(playerB.Trim(), StringComparison.OrdinalIgnoreCase));

        var dto = new TennisH2HDto
        {
            PlayerA = playerA,
            PlayerB = playerB,
            TotalMatches = matches.Count,
            PlayerAWins = aWins,
            PlayerBWins = bWins,
            Matches = matches.Select(m => new TennisH2HMatchDto
            {
                Date = m.MatchDate.HasValue ? MatchDateToUtc(m.MatchDate) : null,
                TourneyName = m.Tournament,
                Surface = m.Surface,
                Round = m.Round,
                Winner = m.Winner,
                Loser = m.Loser,
                Score = FormatSetScore(m),
                WinnerRank = m.WinnerRank,
                LoserRank = m.LoserRank,
            }).ToList()
        };

        return Ok(dto);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DateTime MatchDateToUtc(DateTime? raw)
    {
        if (raw == null) return DateTime.MinValue;
        var d = raw.Value;
        if (d.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return d.ToUniversalTime();
    }

    private static string FormatSetScore(SportsBetting.DataCollector.Core.Entities.HistoricalTennisMatchEntity m)
    {
        var sets = new (short?, short?)[]
        {
            (m.W1, m.L1), (m.W2, m.L2), (m.W3, m.L3), (m.W4, m.L4), (m.W5, m.L5),
        };
        var parts = new List<string>();
        foreach (var (w, l) in sets)
        {
            if (w == null && l == null) continue;
            parts.Add($"{w ?? 0}-{l ?? 0}");
        }
        return parts.Count > 0 ? string.Join(", ", parts) : (m.Comment ?? "");
    }

    /// <summary>
    /// Equivalente a <c>ILIKE</c> com padrão SQL <c>%fragment%</c> (só para listas já materializadas).
    /// </summary>
    private static bool MatchesLikePattern(string? text, string sqlLikePattern)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var core = sqlLikePattern.Trim('%');
        if (string.IsNullOrEmpty(core)) return true;
        return text.Contains(core, StringComparison.OrdinalIgnoreCase);
    }
}
