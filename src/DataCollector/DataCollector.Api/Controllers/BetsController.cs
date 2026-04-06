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
        return Ok(bets.Select(ToBetResultDto));
    }

    /// <summary>
    /// Gets all pending bets (Result == PENDING).
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<BetResultDto>>> GetPending(
        CancellationToken cancellationToken)
    {
        var bets = await _betRepository.GetPendingBetsAsync(cancellationToken);
        return Ok(bets.Select(ToBetResultDto));
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

        RecommendationEntity? recommendation = null;
        if (request.RecommendationId != Guid.Empty)
        {
            recommendation = await _recommendationRepository.GetByIdAsync(
                request.RecommendationId,
                cancellationToken);
        }

        var hasMatchId = request.MatchId is { } mid && mid != Guid.Empty;
        var hasManual = !string.IsNullOrWhiteSpace(request.ManualEventLabel);

        if (recommendation == null && !hasMatchId && !hasManual)
        {
            return BadRequest(new { Message = "Either MatchId or ManualEventLabel is required" });
        }

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
            var selection = !string.IsNullOrWhiteSpace(request.BetSelection)
                ? request.BetSelection.Trim()
                : (!IsBetResultToken(request.Outcome) ? request.Outcome.Trim() : null);
            if (string.IsNullOrWhiteSpace(selection))
            {
                selection = request.Market ?? "N/A";
            }

            var market = string.IsNullOrWhiteSpace(request.Market) ? "N/A" : request.Market.Trim();

            if (hasMatchId)
            {
                var bet = new BetEntity
                {
                    MatchId = request.MatchId,
                    ManualEventLabel = null,
                    ManualSport = null,
                    Market = market,
                    Outcome = selection,
                    Bookmaker = request.Bookmaker,
                    OddPlaced = request.OddActual,
                    StakeEuros = request.StakeActual,
                    Result = "PENDING",
                    ProfitLoss = 0,
                };
                await _betRepository.CreateAsync(bet, cancellationToken);
            }
            else
            {
                var bet = new BetEntity
                {
                    MatchId = null,
                    ManualEventLabel = request.ManualEventLabel!.Trim(),
                    ManualSport = NormalizeManualSport(request.ManualSport),
                    Market = market,
                    Outcome = selection,
                    Bookmaker = request.Bookmaker,
                    OddPlaced = request.OddActual,
                    StakeEuros = request.StakeActual,
                    Result = "PENDING",
                    ProfitLoss = 0,
                };
                await _betRepository.CreateAsync(bet, cancellationToken);
            }
        }

        return Ok(new { Message = "Bet registered successfully" });
    }

    /// <summary>
    /// Settles a pending bet (WIN, LOSS, VOID) and computes P&amp;L from stake and placed odd.
    /// </summary>
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult> Settle(
        Guid id,
        [FromBody] SettleBetRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = (request.Outcome ?? string.Empty).Trim().ToUpperInvariant();
        if (outcome is not ("WIN" or "LOSS" or "VOID"))
        {
            return BadRequest(new { Message = "Outcome must be WIN, LOSS, or VOID" });
        }

        var bet = await _betRepository.GetByIdAsync(id, cancellationToken);
        if (bet == null)
        {
            return NotFound(new { Message = "Bet not found" });
        }

        if (bet.Result != "PENDING")
        {
            return BadRequest(new { Message = "Bet is already settled" });
        }

        var profitLoss = outcome switch
        {
            "WIN" => bet.StakeEuros * (bet.OddPlaced - 1m),
            "LOSS" => -bet.StakeEuros,
            "VOID" => 0m,
            _ => 0m
        };

        bet.Result = outcome;
        bet.ProfitLoss = profitLoss;
        bet.SettledAt = DateTime.UtcNow;
        await _betRepository.UpdateAsync(bet, cancellationToken);

        _logger.LogInformation("Settled bet {BetId} as {Outcome}, P/L {ProfitLoss}", id, outcome, profitLoss);
        return Ok(new { Message = "Bet settled", Outcome = outcome, ProfitLoss = profitLoss });
    }

    private static BetResultDto ToBetResultDto(BetEntity b)
    {
        var isManual = b.Match is null;
        MatchDto matchDto;
        if (b.Match != null)
        {
            matchDto = new MatchDto
            {
                Id = b.Match.Id,
                ExternalId = b.Match.ExternalId,
                Sport = b.Match.Sport,
                CompetitionName = b.Match.Competition?.Name,
                HomeTeam = b.Match.HomeTeam?.Name ?? string.Empty,
                AwayTeam = b.Match.AwayTeam?.Name ?? string.Empty,
                CommenceTime = b.Match.CommenceTime,
                Status = b.Match.Status,
                HomeScore = b.Match.HomeScore,
                AwayScore = b.Match.AwayScore,
                Minute = b.Match.Minute,
            };
        }
        else
        {
            matchDto = new MatchDto
            {
                Id = Guid.Empty,
                ExternalId = "manual",
                Sport = b.ManualSport ?? "manual",
                CompetitionName = null,
                HomeTeam = b.ManualEventLabel ?? "Evento manual",
                AwayTeam = string.Empty,
                CommenceTime = b.PlacedAt,
                Status = "MANUAL",
                HomeScore = null,
                AwayScore = null,
                Minute = null,
            };
        }

        return new BetResultDto
        {
            Id = b.Id,
            MatchId = b.MatchId,
            IsManualEvent = isManual,
            Match = matchDto,
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
                Confidence = b.Recommendation?.Confidence ?? 0,
            },
            StakeActual = b.StakeEuros,
            OddActual = b.OddPlaced,
            Outcome = b.Result ?? "PENDING",
            ProfitLoss = b.ProfitLoss ?? 0,
            SettledAt = b.SettledAt ?? b.PlacedAt,
        };
    }

    private static string NormalizeManualSport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "manual";
        }

        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "football" or "futebol" => "football",
            "tennis" or "ténis" or "tenis" => "tennis",
            _ => "manual",
        };
    }

    private static bool IsBetResultToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var v = value.Trim();
        return v.Equals("WIN", StringComparison.OrdinalIgnoreCase)
               || v.Equals("LOSS", StringComparison.OrdinalIgnoreCase)
               || v.Equals("VOID", StringComparison.OrdinalIgnoreCase)
               || v.Equals("PENDING", StringComparison.OrdinalIgnoreCase);
    }
}
