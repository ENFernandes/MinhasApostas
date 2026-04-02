using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Controllers;

/// <summary>
/// Expõe o estado operacional do sistema para o dashboard do frontend.
/// </summary>
[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    private readonly IMatchRepository _matchRepository;
    private readonly ILogger<SystemController> _logger;

    public SystemController(IMatchRepository matchRepository, ILogger<SystemController> logger)
    {
        _matchRepository = matchRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retorna o estado atual do sistema: jobs a correr, última recolha, jogos hoje e profundidade da queue.
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<SystemStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var matchesToday = await GetMatchesCollectedTodayAsync(cancellationToken);
        var (jobsRunning, lastRun) = GetHangfireStats();
        var queueDepth = GetQueueDepth();

        var overallStatus = jobsRunning > 0 ? "collecting"
            : queueDepth > 0 ? "analysing"
            : "idle";

        return Ok(new SystemStatusDto(
            JobsRunningNow: jobsRunning,
            LastCollectionRun: lastRun,
            MatchesCollectedToday: matchesToday,
            QueueDepth: queueDepth,
            OverallStatus: overallStatus
        ));
    }

    private async Task<int> GetMatchesCollectedTodayAsync(CancellationToken cancellationToken)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var matches = await _matchRepository.GetUpcomingAsync(today, tomorrow, cancellationToken: cancellationToken);
            return matches.Count();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to count today's matches");
            return 0;
        }
    }

    private static (int Running, DateTimeOffset? LastRun) GetHangfireStats()
    {
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var processing = monitor.ProcessingJobs(0, 10);
            var succeeded = monitor.SucceededJobs(0, 1);
            var lastRun = succeeded.FirstOrDefault().Value?.SucceededAt;
            return (processing.Count, lastRun.HasValue ? new DateTimeOffset(lastRun.Value) : null);
        }
        catch
        {
            return (0, null);
        }
    }

    private static int GetQueueDepth()
    {
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var queues = monitor.Queues();
            return (int)queues.Sum(q => q.Length);
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Estado operacional do sistema de recolha de dados.
/// </summary>
/// <param name="JobsRunningNow">Número de jobs Hangfire em execução neste momento.</param>
/// <param name="LastCollectionRun">Timestamp do último job concluído com sucesso.</param>
/// <param name="MatchesCollectedToday">Número de jogos recolhidos para hoje.</param>
/// <param name="QueueDepth">Número total de jobs pendentes nas queues Hangfire.</param>
/// <param name="OverallStatus">Estado geral: idle | collecting | analysing.</param>
public record SystemStatusDto(
    int JobsRunningNow,
    DateTimeOffset? LastCollectionRun,
    int MatchesCollectedToday,
    int QueueDepth,
    string OverallStatus
);
