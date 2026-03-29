using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for betting recommendations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly ILogger<RecommendationsController> _logger;
    private readonly IRecommendationRepository _recommendationRepository;

    public RecommendationsController(
        ILogger<RecommendationsController> logger,
        IRecommendationRepository recommendationRepository)
    {
        _logger = logger;
        _recommendationRepository = recommendationRepository;
    }

    /// <summary>
    /// Gets all recommendations with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecommendationDto>>> GetAll(
        [FromQuery] GetRecommendationsRequest request,
        CancellationToken cancellationToken)
    {
        var recommendations = await _recommendationRepository.GetAllAsync(
            request.Status, 
            request.Sport, 
            cancellationToken);
        
        var dtos = recommendations.Select(r => new RecommendationDto
        {
            Id = r.Id,
            MatchId = r.MatchId,
            Market = r.Market,
            Outcome = r.Outcome,
            Bookmaker = r.Bookmaker ?? string.Empty,
            OddDecimal = r.OddDecimal,
            ModelProbability = r.ModelProbability,
            ImpliedProbability = r.ImpliedProbability ?? 0,
            Value = r.Value ?? 0,
            KellyFraction = r.KellyFraction ?? 0,
            StakeEuros = r.StakeEuros ?? 0,
            Confidence = r.Confidence ?? 0,
            Reasoning = r.Reasoning,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        });
        
        return Ok(dtos);
    }

    /// <summary>
    /// Creates a new recommendation from the analysis engine.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RecommendationDto>> Create(
        [FromBody] CreateRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating recommendation for match {MatchId}", request.MatchId);

        var entity = new RecommendationEntity
        {
            MatchId = request.MatchId,
            Market = request.Market,
            Outcome = request.Outcome,
            Bookmaker = request.Bookmaker,
            OddDecimal = request.OddDecimal,
            ModelProbability = request.ModelProbability,
            ImpliedProbability = request.ImpliedProbability,
            Value = request.Value,
            KellyFraction = request.KellyFraction,
            StakeEuros = request.StakeEuros,
            Confidence = request.Confidence,
            Reasoning = request.Reasoning,
            Status = "PENDING"
        };

        var created = await _recommendationRepository.CreateAsync(entity, cancellationToken);

        var dto = new RecommendationDto
        {
            Id = created.Id,
            MatchId = created.MatchId,
            Market = created.Market,
            Outcome = created.Outcome,
            Bookmaker = created.Bookmaker ?? string.Empty,
            OddDecimal = created.OddDecimal,
            ModelProbability = created.ModelProbability,
            ImpliedProbability = created.ImpliedProbability ?? 0,
            Value = created.Value ?? 0,
            KellyFraction = created.KellyFraction ?? 0,
            StakeEuros = created.StakeEuros ?? 0,
            Confidence = created.Confidence ?? 0,
            Reasoning = created.Reasoning,
            Status = created.Status,
            CreatedAt = created.CreatedAt
        };

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    /// <summary>
    /// Gets a recommendation by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecommendationDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var recommendation = await _recommendationRepository.GetByIdAsync(id, cancellationToken);
        
        if (recommendation == null)
        {
            return NotFound();
        }

        return Ok(new RecommendationDto
        {
            Id = recommendation.Id,
            MatchId = recommendation.MatchId,
            Market = recommendation.Market,
            Outcome = recommendation.Outcome,
            Bookmaker = recommendation.Bookmaker ?? string.Empty,
            OddDecimal = recommendation.OddDecimal,
            ModelProbability = recommendation.ModelProbability,
            ImpliedProbability = recommendation.ImpliedProbability ?? 0,
            Value = recommendation.Value ?? 0,
            KellyFraction = recommendation.KellyFraction ?? 0,
            StakeEuros = recommendation.StakeEuros ?? 0,
            Confidence = recommendation.Confidence ?? 0,
            Reasoning = recommendation.Reasoning,
            Status = recommendation.Status,
            CreatedAt = recommendation.CreatedAt
        });
    }
}
