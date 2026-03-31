using Microsoft.Extensions.DependencyInjection;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Services;

/// <summary>
/// Hangfire-compatible dispatcher that resolves and executes IJobService implementations via DI.
/// </summary>
public class JobDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public JobDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Resolves the job type from its assembly-qualified name and executes it.
    /// Called by Hangfire recurring job scheduler.
    /// </summary>
    public async Task ExecuteAsync(string jobTypeAssemblyQualifiedName)
    {
        var jobType = Type.GetType(jobTypeAssemblyQualifiedName)
            ?? throw new InvalidOperationException($"Job type not found: {jobTypeAssemblyQualifiedName}");

        using var scope = _serviceProvider.CreateScope();
        var job = (IJobService)ActivatorUtilities.CreateInstance(scope.ServiceProvider, jobType);
        await job.ExecuteAsync(CancellationToken.None);
    }
}
