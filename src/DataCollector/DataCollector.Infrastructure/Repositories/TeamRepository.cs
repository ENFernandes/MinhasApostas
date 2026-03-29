using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for teams.
/// </summary>
public class TeamRepository : ITeamRepository, IScopedService
{
    private readonly SportsBettingDbContext _context;

    public TeamRepository(SportsBettingDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<TeamEntity?> GetByExternalIdAsync(string externalId, string sport, CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .FirstOrDefaultAsync(t => t.ExternalId == externalId && t.Sport == sport, cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(TeamEntity team, CancellationToken cancellationToken = default)
    {
        var existing = await GetByExternalIdAsync(team.ExternalId, team.Sport, cancellationToken);

        if (existing is null)
        {
            team.Id = Guid.NewGuid();
            team.CreatedAt = DateTime.UtcNow;
            _context.Teams.Add(team);
        }
        else
        {
            existing.Name = team.Name;
            existing.ShortName = team.ShortName;
            existing.Country = team.Country;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.Teams.Update(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
