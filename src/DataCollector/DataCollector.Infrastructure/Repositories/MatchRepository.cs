using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for matches.
/// </summary>
public class MatchRepository : IMatchRepository
{
    private readonly SportsBettingDbContext _context;

    public MatchRepository(SportsBettingDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<MatchEntity?> GetByExternalIdAsync(string externalId, string sport, CancellationToken cancellationToken = default)
    {
        return await _context.Matches
            .FirstOrDefaultAsync(m => m.ExternalId == externalId && m.Sport == sport, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MatchEntity>> GetUpcomingAsync(
        DateTime from,
        DateTime to,
        string? sport = null,
        bool onlyWithOdds = false,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.Matches
            .Include(m => m.Competition)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.CommenceTime <= to)
            .Where(m =>
                // Jogos ao vivo: mostrar sempre independente da hora
                (m.Status == "LIVE" || m.Status == "IN_PLAY" || m.Status == "PAUSED")
                ||
                // Jogos agendados: só mostrar se ainda não começaram (margem 30 min para atrasos)
                ((m.Status == "SCHEDULED" || m.Status == null || m.Status == "")
                 && m.CommenceTime >= now.AddMinutes(-30))
            );

        if (!string.IsNullOrEmpty(sport))
        {
            query = query.Where(m => m.Sport == sport);
        }

        if (onlyWithOdds)
        {
            query = query.Where(m => _context.Odds.Any(o => o.MatchId == m.Id));
        }

        return await query.OrderBy(m => m.CommenceTime).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MatchEntity>> GetLiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Matches
            .Where(m => m.Status == "LIVE" || m.Status == "IN_PLAY")
            .OrderBy(m => m.CommenceTime)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MatchEntity?> FindByTeamNamesAndDateAsync(string homeTeam, string awayTeam, DateTime commenceTime, CancellationToken cancellationToken = default)
    {
        // Janela de ±2 horas para cobrir diferenças de sincronização
        var windowStart = commenceTime.AddHours(-2);
        var windowEnd = commenceTime.AddHours(2);

        var candidates = await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.CommenceTime >= windowStart && m.CommenceTime <= windowEnd)
            .ToListAsync(cancellationToken);

        // Match fuzzy por nome de equipa (case-insensitive, contains em ambas as direções)
        var matchNormal = candidates.FirstOrDefault(m =>
            m.HomeTeam != null && m.AwayTeam != null &&
            NamesFuzzyMatch(m.HomeTeam.Name, homeTeam) &&
            NamesFuzzyMatch(m.AwayTeam.Name, awayTeam));

        if (matchNormal is not null)
            return matchNormal;

        // Fallback: odds providers may invert home/away ordering, especially for tennis events
        return candidates.FirstOrDefault(m =>
            m.HomeTeam != null && m.AwayTeam != null &&
            NamesFuzzyMatch(m.HomeTeam.Name, awayTeam) &&
            NamesFuzzyMatch(m.AwayTeam.Name, homeTeam));
    }

    private static bool NamesFuzzyMatch(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        if (a.Contains(b, StringComparison.OrdinalIgnoreCase) ||
            b.Contains(a, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Tennis providers often abbreviate names (e.g. "A. Zakharova" vs "Anastasia Zakharova").
        // Try a light normalization based on last-name + first-initial (supports doubles via "/").
        var na = NormalizeTennisParticipantKey(a);
        var nb = NormalizeTennisParticipantKey(b);
        return !string.IsNullOrWhiteSpace(na) &&
               !string.IsNullOrWhiteSpace(nb) &&
               string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTennisParticipantKey(string name)
    {
        // Split doubles like "Player1/ Player2" into stable sorted keys.
        var parts = name
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeSingleTennisName)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parts.Count == 0)
            return string.Empty;

        return string.Join("|", parts);
    }

    private static string NormalizeSingleTennisName(string name)
    {
        // Remove punctuation and normalize whitespace.
        var cleaned = new string(name
            .Trim()
            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))
            .ToArray());

        var tokens = cleaned
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            return string.Empty;

        var last = tokens[^1];
        var first = tokens[0];
        var initial = first.Length > 0 ? first[0].ToString().ToLowerInvariant() : string.Empty;

        return $"{initial}{last}".ToLowerInvariant();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<MatchEntity>> GetFinishedSinceAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        return await _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Where(m => m.Status == "FINISHED"
                     && m.UpdatedAt >= since
                     && m.Sport == "football")
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(MatchEntity match, CancellationToken cancellationToken = default)
    {
        var existing = await GetByExternalIdAsync(match.ExternalId, match.Sport, cancellationToken);

        if (existing is null)
        {
            match.Id = Guid.NewGuid();
            match.CreatedAt = DateTime.UtcNow;
            match.UpdatedAt = DateTime.UtcNow;
            _context.Matches.Add(match);
        }
        else
        {
            existing.HomeScore = match.HomeScore;
            existing.AwayScore = match.AwayScore;
            existing.HomeXg = match.HomeXg;
            existing.AwayXg = match.AwayXg;
            existing.Minute = match.Minute;
            existing.Status = match.Status;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Matches.Update(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
