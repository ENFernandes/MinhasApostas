using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for performance statistics.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly ILogger<StatsController> _logger;
    private readonly IBetRepository _betRepository;
    private readonly IRecommendationRepository _recommendationRepository;

    public StatsController(
        ILogger<StatsController> logger,
        IBetRepository betRepository,
        IRecommendationRepository recommendationRepository)
    {
        _logger = logger;
        _betRepository = betRepository;
        _recommendationRepository = recommendationRepository;
    }

    /// <summary>
    /// Gets overall performance statistics (P&L, ROI, win rate).
    /// </summary>
    [HttpGet("performance")]
    public async Task<ActionResult<PerformanceStatsDto>> GetPerformance(
        CancellationToken cancellationToken)
    {
        var stats = await _betRepository.GetPerformanceStatsAsync(cancellationToken);
        
        return Ok(new PerformanceStatsDto
        {
            TotalProfitLoss = stats.TotalProfitLoss,
            Roi = stats.Roi,
            WinRate = stats.WinRate,
            TotalBets = stats.TotalBets,
            WinningBets = stats.WinningBets,
            LosingBets = stats.LosingBets,
            ByMarket = stats.ByMarket.Select(m => new PerformanceByMarketDto
            {
                Market = m.Market,
                TotalBets = m.TotalBets,
                WinRate = m.WinRate,
                ProfitLoss = m.ProfitLoss,
                Roi = m.Roi
            }).ToList(),
            BySport = stats.BySport.Select(s => new PerformanceBySportDto
            {
                Sport = s.Sport,
                TotalBets = s.TotalBets,
                WinRate = s.WinRate,
                ProfitLoss = s.ProfitLoss,
                Roi = s.Roi
            }).ToList()
        });
    }

    /// <summary>
    /// Calcula performance das recomendações da IA com base nos resultados registados.
    /// </summary>
    [HttpGet("ai-performance")]
    public async Task<ActionResult<AiPerformanceDto>> GetAiPerformance(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        var resolved = (await _recommendationRepository.GetResolvedAsync(days, cancellationToken)).ToList();

        if (resolved.Count == 0)
        {
            return Ok(new AiPerformanceDto());
        }

        var wins = resolved.Count(r => r.BetOutcome == "WON");
        var losses = resolved.Count(r => r.BetOutcome == "LOST");
        var voids = resolved.Count(r => r.BetOutcome == "VOID");
        var settled = wins + losses; // VOID não conta para win rate

        var totalStaked = resolved.Where(r => r.BetOutcome != "VOID").Sum(r => r.StakeEuros ?? 0);
        var totalReturns = resolved
            .Where(r => r.BetOutcome == "WON")
            .Sum(r => (r.StakeEuros ?? 0) * r.OddDecimal);
        var totalProfit = totalReturns - totalStaked;

        var kellyProfit = resolved
            .Where(r => r.BetOutcome != "VOID")
            .Sum(r => r.BetOutcome == "WON"
                ? (r.StakeEuros ?? 0) * r.OddDecimal - (r.StakeEuros ?? 0)
                : -(r.StakeEuros ?? 0));

        var (bestStreak, worstStreak) = CalculateStreaks(resolved
            .Where(r => r.BetOutcome != "VOID")
            .Select(r => r.BetOutcome == "WON"));

        return Ok(new AiPerformanceDto
        {
            TotalResolved = resolved.Count,
            Wins = wins,
            Losses = losses,
            Voids = voids,
            WinRate = settled > 0 ? (double)wins / settled : 0,
            Yield = totalStaked > 0 ? (double)(totalProfit / totalStaked) : 0,
            TotalStaked = totalStaked,
            TotalProfit = totalProfit,
            BestStreak = bestStreak,
            WorstStreak = worstStreak,
            SimulatedKellyProfit = kellyProfit,
        });
    }

    private static (int Best, int Worst) CalculateStreaks(IEnumerable<bool> results)
    {
        var best = 0;
        var worst = 0;
        var current = 0;

        foreach (var won in results)
        {
            current = won ? (current >= 0 ? current + 1 : 1) : (current <= 0 ? current - 1 : -1);
            if (current > best) best = current;
            if (current < worst) worst = current;
        }

        return (best, Math.Abs(worst));
    }
}
