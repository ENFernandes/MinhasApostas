using Microsoft.EntityFrameworkCore;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;
using SportsBetting.DataCollector.Infrastructure.Data;

namespace SportsBetting.DataCollector.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for recommendations.
/// </summary>
public class RecommendationRepository : IRecommendationRepository, IScopedService
{
    private readonly SportsBettingDbContext _context;

    public RecommendationRepository(SportsBettingDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RecommendationEntity>> GetAllAsync(
        string? status = null, 
        string? sport = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Recommendations
            .Include(r => r.Match)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (!string.IsNullOrEmpty(sport))
        {
            query = query.Where(r => r.Match != null && r.Match.Sport == sport);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RecommendationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Recommendations
            .Include(r => r.Match)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RecommendationEntity> CreateAsync(RecommendationEntity recommendation, CancellationToken cancellationToken = default)
    {
        recommendation.Id = Guid.NewGuid();
        recommendation.CreatedAt = DateTime.UtcNow;
        recommendation.UpdatedAt = DateTime.UtcNow;
        
        _context.Recommendations.Add(recommendation);
        await _context.SaveChangesAsync(cancellationToken);
        
        return recommendation;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(RecommendationEntity recommendation, CancellationToken cancellationToken = default)
    {
        recommendation.UpdatedAt = DateTime.UtcNow;
        _context.Recommendations.Update(recommendation);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
