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

    /// <summary>
    /// Regista o resultado de uma aposta (WON, LOST ou VOID).
    /// </summary>
    Task<RecommendationEntity?> UpdateOutcomeAsync(
        Guid id,
        string outcome,
        string source = "manual",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna todas as recomendações com resultado registado para cálculo de performance.
    /// </summary>
    Task<IEnumerable<RecommendationEntity>> GetResolvedAsync(
        int days = 30,
        CancellationToken cancellationToken = default);
}
