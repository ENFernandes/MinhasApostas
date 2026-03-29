using SportsBetting.DataCollector.Core.Entities;

namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Repository interface for recommendations.
/// </summary>
public interface IRecommendationRepository
{
    /// <summary>
    /// Gets all recommendations with optional filtering.
    /// </summary>
    Task<IEnumerable<RecommendationEntity>> GetAllAsync(
        string? status = null, 
        string? sport = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a recommendation by ID.
    /// </summary>
    Task<RecommendationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Creates a new recommendation.
    /// </summary>
    Task<RecommendationEntity> CreateAsync(RecommendationEntity recommendation, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a recommendation.
    /// </summary>
    Task UpdateAsync(RecommendationEntity recommendation, CancellationToken cancellationToken = default);
}
