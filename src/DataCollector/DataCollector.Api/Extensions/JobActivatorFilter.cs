using Hangfire.Client;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Extensions;

/// <summary>
/// Hangfire filter that resolves job services from DI container.
/// </summary>
public class JobActivatorFilter : JobFilterAttribute, IClientFilter, IServerFilter
{
    public void OnCreating(CreatingContext context)
    {
        // Called before job is created
    }

    public void OnCreated(CreatedContext context)
    {
        // Called after job is created
    }

    public void OnPerforming(PerformingContext context)
    {
        // Called before job execution
        var jobType = context.BackgroundJob.Job.Type;
        
        if (typeof(IJobService).IsAssignableFrom(jobType))
        {
            // The job will be resolved by the activator
            Console.WriteLine($"Performing job: {jobType.Name}");
        }
    }

    public void OnPerformed(PerformedContext context)
    {
        // Called after job execution
        Console.WriteLine($"Job completed: {context.BackgroundJob.Job.Type.Name}");
    }
}
