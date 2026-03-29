using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Api.Dtos;
using SportsBetting.DataCollector.Core.Entities;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// API endpoints for match data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MatchesController : ControllerBase
{
    private readonly IMatchRepository _matchRepository;
    private readonly IOddsRepository _oddsRepository;
    private readonly ILogger<MatchesController> _logger;

    public MatchesController(
        IMatchRepository matchRepository,
        IOddsRepository oddsRepository,
        ILogger<MatchesController> logger)
    {
        _matchRepository = matchRepository;
        _oddsRepository = oddsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets upcoming matches with optional filtering.
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<MatchDto>>> GetUpcoming(
        [FromQuery] GetUpcomingMatchesRequest request,
        CancellationToken cancellationToken)
    {
        var from = request.From ?? DateTime.UtcNow;
        var to = request.To ?? DateTime.UtcNow.AddDays(1);

        var matches = await _matchRepository.GetUpcomingAsync(from, to, request.Sport, cancellationToken);

        var dtos = matches.Select(m => new MatchDto
        {
            Id = m.Id,
            ExternalId = m.ExternalId,
            Sport = m.Sport,
            CompetitionName = m.Competition != null ? m.Competition.Name : null,
            HomeTeam = m.HomeTeam != null ? m.HomeTeam.Name : string.Empty,
            AwayTeam = m.AwayTeam != null ? m.AwayTeam.Name : string.Empty,
            // Garantir DateTimeKind.Utc para serialização correta com 'Z' no JSON
            CommenceTime = DateTime.SpecifyKind(m.CommenceTime, DateTimeKind.Utc),
            Status = m.Status,
            HomeScore = m.HomeScore,
            AwayScore = m.AwayScore,
            Minute = m.Minute
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Gets detailed information for a specific match.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MatchDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        // This would need a GetById method in the repository
        // For now, return a placeholder
        return Ok(new MatchDetailDto { Id = id });
    }

    /// <summary>
    /// Gets all odds for a specific match.
    /// </summary>
    [HttpGet("{id:guid}/odds")]
    public async Task<ActionResult<IEnumerable<OddsDto>>> GetOdds(
        Guid id,
        CancellationToken cancellationToken)
    {
        var odds = await _oddsRepository.GetByMatchAsync(id, cancellationToken);

        var dtos = odds.Select(o => new OddsDto
        {
            Bookmaker = o.Bookmaker,
            Market = o.Market,
            Outcome = o.Outcome,
            OddDecimal = o.OddDecimal,
            ImpliedProbability = o.ImpliedProbability,
            CapturedAt = o.CapturedAt
        });

        return Ok(dtos);
    }
}
