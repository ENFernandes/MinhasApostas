namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Marker interface for Hangfire job services.
/// Implementations are automatically registered and scheduled.
/// </summary>
public interface IJobService : IScopedService
{
    /// <summary>
    /// Gets the cron expression for scheduling this job.
    /// </summary>
    string CronExpression { get; }

    /// <summary>
    /// Gets the unique job identifier.
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Executes the job logic.
    /// </summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
