using Microsoft.AspNetCore.Mvc;
using Hangfire;

namespace SportsBetting.DataCollector.Api.Controllers;

[ApiController]
[Route("api/collector")]
public class CollectorController : ControllerBase
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<CollectorController> _logger;

    public CollectorController(
        IBackgroundJobClient backgroundJobClient,
        ILogger<CollectorController> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    /// <summary>
    /// Manually triggers the football data collection job.
    /// </summary>
    [HttpPost("collect/football")]
    public ActionResult TriggerFootballCollection()
    {
        _backgroundJobClient.Enqueue<Infrastructure.Jobs.FootballCollectorJob>(job => job.ExecuteAsync(default));
        _logger.LogInformation("Football collection job triggered manually");
        return Ok(new { message = "Football collection job triggered" });
    }

    /// <summary>
    /// Manually triggers the tennis data collection job.
    /// </summary>
    [HttpPost("collect/tennis")]
    public ActionResult TriggerTennisCollection()
    {
        _backgroundJobClient.Enqueue<Infrastructure.Jobs.TennisCollectorJob>(job => job.ExecuteAsync(default));
        _logger.LogInformation("Tennis collection job triggered manually");
        return Ok(new { message = "Tennis collection job triggered" });
    }

    /// <summary>
    /// Manually triggers the odds collection job.
    /// </summary>
    [HttpPost("collect/odds")]
    public ActionResult TriggerOddsCollection()
    {
        _backgroundJobClient.Enqueue<Infrastructure.Jobs.OddsCollectorJob>(job => job.ExecuteAsync(default));
        _logger.LogInformation("Odds collection job triggered manually");
        return Ok(new { message = "Odds collection job triggered" });
    }
}
