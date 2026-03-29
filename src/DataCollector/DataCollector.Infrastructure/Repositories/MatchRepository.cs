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
    public async Task<IEnumerable<MatchEntity>> GetUpcomingAsync(DateTime from, DateTime to, string? sport = null, CancellationToken cancellationToken = default)
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
        return candidates.FirstOrDefault(m =>
            m.HomeTeam != null && m.AwayTeam != null &&
            (m.HomeTeam.Name.Contains(homeTeam, StringComparison.OrdinalIgnoreCase) ||
             homeTeam.Contains(m.HomeTeam.Name, StringComparison.OrdinalIgnoreCase)) &&
            (m.AwayTeam.Name.Contains(awayTeam, StringComparison.OrdinalIgnoreCase) ||
             awayTeam.Contains(m.AwayTeam.Name, StringComparison.OrdinalIgnoreCase)));
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
