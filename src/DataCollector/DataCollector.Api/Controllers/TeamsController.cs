using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Core.Dtos;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for team data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ILogger<TeamsController> _logger;
    private readonly ITeamStatsService _teamStatsService;

    public TeamsController(
        ILogger<TeamsController> logger,
        ITeamStatsService teamStatsService)
    {
        _logger = logger;
        _teamStatsService = teamStatsService;
    }

    /// <summary>
    /// Gets recent form for a team by name.
    /// </summary>
    [HttpGet("form")]
    public async Task<ActionResult<TeamFormDto>> GetFormByName(
        [FromQuery] string teamName,
        [FromQuery] string sport = "football",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(teamName))
        {
            return BadRequest("teamName is required");
        }

        var form = await _teamStatsService.GetTeamFormAsync(teamName, sport, cancellationToken);
        return Ok(form);
    }

    /// <summary>
    /// Gets recent form for a team.
    /// </summary>
    [HttpGet("{id:guid}/form")]
    public async Task<ActionResult<TeamFormDto>> GetForm(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Placeholder - would fetch from repository
        return Ok(new TeamFormDto
        {
            TeamName = "Team Name",
            Last10Games = new List<MatchResultDto>(),
            AvgGoalsScoredHome = 1.5f,
            AvgGoalsScoredAway = 1.2f,
            AvgGoalsConcededHome = 1.0f,
            AvgGoalsConcededAway = 1.3f
        });
    }

    /// <summary>
    /// Gets head-to-head stats between two teams.
    /// </summary>
    [HttpGet("head-to-head")]
    public async Task<ActionResult<HeadToHeadDto>> GetHeadToHead(
        [FromQuery] string homeTeam,
        [FromQuery] string awayTeam,
        [FromQuery] string sport = "football",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(homeTeam) || string.IsNullOrEmpty(awayTeam))
        {
            return BadRequest("homeTeam and awayTeam are required");
        }

        var h2h = await _teamStatsService.GetHeadToHeadAsync(homeTeam, awayTeam, sport, cancellationToken);
        return Ok(h2h);
    }
}
