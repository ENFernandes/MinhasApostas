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
        
        var dtos = recommendations.Select(ToDto);
        
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
        var dto = ToDto(created);
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

        return Ok(ToDto(recommendation));
    }

    /// <summary>
    /// Regista o resultado de uma aposta: WON, LOST ou VOID.
    /// </summary>
    [HttpPatch("{id:guid}/outcome")]
    public async Task<ActionResult<RecommendationDto>> RecordOutcome(
        Guid id,
        [FromBody] RecordOutcomeRequest request,
        CancellationToken cancellationToken)
    {
        var valid = new[] { "WON", "LOST", "VOID" };
        if (!valid.Contains(request.Outcome.ToUpperInvariant()))
        {
            return BadRequest(new { error = "Outcome must be WON, LOST or VOID" });
        }

        var updated = await _recommendationRepository.UpdateOutcomeAsync(
            id, request.Outcome, "manual", cancellationToken);

        if (updated is null) return NotFound();

        _logger.LogInformation(
            "Recommendation outcome recorded: {Id} → {Outcome}", id, request.Outcome);

        return Ok(ToDto(updated));
    }

    private static RecommendationDto ToDto(RecommendationEntity r) => new()
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
        BetOutcome = r.BetOutcome,
        OutcomeRecordedAt = r.OutcomeRecordedAt,
        CreatedAt = r.CreatedAt
    };
}
