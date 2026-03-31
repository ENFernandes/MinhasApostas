using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for bet tracking and results.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BetsController : ControllerBase
{
    private readonly ILogger<BetsController> _logger;
    private readonly IBetRepository _betRepository;
    private readonly IRecommendationRepository _recommendationRepository;

    public BetsController(
        ILogger<BetsController> logger,
        IBetRepository betRepository,
        IRecommendationRepository recommendationRepository)
    {
        _logger = logger;
        _betRepository = betRepository;
        _recommendationRepository = recommendationRepository;
    }

    /// <summary>
    /// Gets all settled bets (bet history).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BetResultDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var bets = await _betRepository.GetSettledBetsAsync(cancellationToken);
        
        var dtos = bets.Select(b => new BetResultDto
        {
            Id = b.Id,
            MatchId = b.MatchId,
            Match = b.Match != null ? new MatchDto
            {
                Id = b.Match.Id,
                ExternalId = b.Match.ExternalId,
                Sport = b.Match.Sport,
                HomeTeam = b.Match.HomeTeam?.Name ?? string.Empty,
                AwayTeam = b.Match.AwayTeam?.Name ?? string.Empty,
                CommenceTime = b.Match.CommenceTime,
                Status = b.Match.Status,
                HomeScore = b.Match.HomeScore,
                AwayScore = b.Match.AwayScore,
                Minute = b.Match.Minute
            } : null!,
            Recommendation = new RecommendedMarketDto
            {
                Market = b.Market,
                Outcome = b.Outcome,
                Bookmaker = b.Bookmaker ?? string.Empty,
                Odd = b.OddPlaced,
                ImpliedProbability = 0, // Would need to calculate
                ModelProbability = 0,   // Would need to get from recommendation
                Value = 0,              // Would need to calculate
                KellyFraction = 0,      // Would need to get from recommendation
                StakeEuros = b.StakeEuros,
                Confidence = b.Recommendation?.Confidence ?? 0
            },
            StakeActual = b.StakeEuros,
            OddActual = b.OddPlaced,
            Outcome = b.Result ?? "PENDING",
            ProfitLoss = b.ProfitLoss ?? 0,
            SettledAt = b.SettledAt ?? b.PlacedAt
        });
        
        return Ok(dtos);
    }

    /// <summary>
    /// Gets all pending bets (Result == PENDING).
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<BetResultDto>>> GetPending(
        CancellationToken cancellationToken)
    {
        var bets = await _betRepository.GetPendingBetsAsync(cancellationToken);

        var dtos = bets.Select(b => new BetResultDto
        {
            Id = b.Id,
            MatchId = b.MatchId,
            Match = b.Match != null ? new MatchDto
            {
                Id = b.Match.Id,
                ExternalId = b.Match.ExternalId,
                Sport = b.Match.Sport,
                HomeTeam = b.Match.HomeTeam?.Name ?? string.Empty,
                AwayTeam = b.Match.AwayTeam?.Name ?? string.Empty,
                CommenceTime = b.Match.CommenceTime,
                Status = b.Match.Status,
                HomeScore = b.Match.HomeScore,
                AwayScore = b.Match.AwayScore,
                Minute = b.Match.Minute
            } : null!,
            Recommendation = new RecommendedMarketDto
            {
                Market = b.Market,
                Outcome = b.Outcome,
                Bookmaker = b.Bookmaker ?? string.Empty,
                Odd = b.OddPlaced,
                ImpliedProbability = 0,
                ModelProbability = 0,
                Value = 0,
                KellyFraction = 0,
                StakeEuros = b.StakeEuros,
                Confidence = b.Recommendation?.Confidence ?? 0
            },
            StakeActual = b.StakeEuros,
            OddActual = b.OddPlaced,
            Outcome = b.Result ?? "PENDING",
            ProfitLoss = b.ProfitLoss ?? 0,
            SettledAt = b.SettledAt ?? b.PlacedAt
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Registers the result of a bet.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Register(
        [FromBody] RegisterBetRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Registering bet result for recommendation {RecommendationId}: {Outcome}",
            request.RecommendationId,
            request.Outcome);

        // Try to get the recommendation
        RecommendationEntity? recommendation = null;
        if (request.RecommendationId != Guid.Empty)
        {
            recommendation = await _recommendationRepository.GetByIdAsync(
                request.RecommendationId,
                cancellationToken);
        }

        if (recommendation == null && request.MatchId == null)
        {
            return BadRequest(new { Message = "Either RecommendationId or MatchId is required" });
        }

        // Check if bet already exists for this recommendation
        if (recommendation != null)
        {
            var existingBet = await _betRepository.GetByRecommendationIdAsync(
                request.RecommendationId,
                cancellationToken);

            if (existingBet != null)
            {
                existingBet.Result = request.Outcome == "PENDING" ? "PENDING" : request.Outcome;
                existingBet.ProfitLoss = request.ProfitLoss;
                existingBet.SettledAt = request.Outcome != "PENDING" ? DateTime.UtcNow : null;
                await _betRepository.UpdateAsync(existingBet, cancellationToken);
            }
            else
            {
                var bet = new BetEntity
                {
                    RecommendationId = request.RecommendationId,
                    MatchId = recommendation.MatchId,
                    Market = recommendation.Market,
                    Outcome = recommendation.Outcome,
                    Bookmaker = recommendation.Bookmaker,
                    OddPlaced = request.OddActual,
                    StakeEuros = request.StakeActual,
                    Result = request.Outcome == "PENDING" ? "PENDING" : request.Outcome,
                    ProfitLoss = request.ProfitLoss,
                    SettledAt = request.Outcome != "PENDING" ? DateTime.UtcNow : null,
                };
                await _betRepository.CreateAsync(bet, cancellationToken);
            }

            recommendation.Status = request.Outcome == "PENDING" ? "ACCEPTED" : "SETTLED";
            await _recommendationRepository.UpdateAsync(recommendation, cancellationToken);
        }
        else
        {
            // Direct bet creation without a recommendation
            var bet = new BetEntity
            {
                MatchId = request.MatchId!.Value,
                Market = request.Market ?? "N/A",
                Outcome = request.Outcome == "PENDING" ? (request.Market ?? "N/A") : request.Outcome,
                Bookmaker = request.Bookmaker,
                OddPlaced = request.OddActual,
                StakeEuros = request.StakeActual,
                Result = "PENDING",
                ProfitLoss = 0,
            };
            await _betRepository.CreateAsync(bet, cancellationToken);
        }

        return Ok(new { Message = "Bet registered successfully" });
    }
}
