using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for player data (tennis).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(ILogger<PlayersController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets statistics for a tennis player including ELO ratings.
    /// </summary>
    [HttpGet("{id:guid}/stats")]
    public async Task<ActionResult<PlayerStatsDto>> GetStats(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Placeholder - would fetch from repository
        return Ok(new PlayerStatsDto
        {
            Name = "Player Name",
            Ranking = 10,
            EloOverall = 1800,
            EloClay = 1850,
            EloGrass = 1750,
            EloHard = 1820,
            EloIndoor = 1780
        });
    }
}
