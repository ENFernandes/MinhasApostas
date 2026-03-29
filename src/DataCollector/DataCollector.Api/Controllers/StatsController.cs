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

    public StatsController(ILogger<StatsController> logger, IBetRepository betRepository)
    {
        _logger = logger;
        _betRepository = betRepository;
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
}
