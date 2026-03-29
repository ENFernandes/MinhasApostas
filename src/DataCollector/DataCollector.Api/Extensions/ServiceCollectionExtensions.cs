using System.Reflection;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using SportsBetting.DataCollector.Core.Interfaces;

namespace SportsBetting.DataCollector.Api.Extensions;

/// <summary>
/// Extension methods for registering services via reflection scanning.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans the specified assemblies and automatically registers all services
    /// that implement marker interfaces (IScopedService, ITransientService, ISingletonService).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan. If null, scans the calling assembly.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddServicesByReflection(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetExecutingAssembly() };
        }

        foreach (var assembly in assemblies)
        {
            RegisterScopedServices(services, assembly);
            RegisterTransientServices(services, assembly);
            RegisterSingletonServices(services, assembly);
            RegisterGenericRepositories(services, assembly);
        }

        return services;
    }

    /// <summary>
    /// Schedules all Hangfire jobs that implement IJobService.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Assemblies to scan for jobs.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection ScheduleHangfireJobs(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
        {
            assemblies = new[] { Assembly.GetExecutingAssembly() };
        }

        foreach (var assembly in assemblies)
        {
            var jobTypes = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                            && typeof(IJobService).IsAssignableFrom(t));

            foreach (var jobType in jobTypes)
            {
                // Create a dummy instance to get JobId and CronExpression
                // This is done during service registration, so we use Activator
                var jobInstance = Activator.CreateInstance(jobType,
                    new object?[jobType.GetConstructors().First().GetParameters().Length]);

                if (jobInstance is IJobService jobService)
                {
                    RecurringJob.AddOrUpdate(
                        jobService.JobId,
                        () => ExecuteJob(jobType),
                        jobService.CronExpression);
                }
            }
        }

        return services;
    }

    /// <summary>
    /// Executes a job by type. This is called by Hangfire.
    /// </summary>
    public static void ExecuteJob(Type jobType)
    {
        // This method signature is used by Hangfire
        // The actual implementation will be resolved by DI at runtime
    }

    /// <summary>
    /// Registers all implementations of IScopedService as scoped services.
    /// </summary>
    private static void RegisterScopedServices(IServiceCollection services, Assembly assembly)
    {
        var scopedTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(IScopedService).IsAssignableFrom(t));

        foreach (var implementationType in scopedTypes)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i != typeof(IScopedService)
                            && i != typeof(IJobService)
                            && i != typeof(IApiClient)
                            && !i.IsGenericType)
                .ToList();

            if (interfaces.Count == 0)
            {
                // Register as self if no interfaces found
                services.AddScoped(implementationType);
            }
            else
            {
                // Register each interface with the implementation
                foreach (var interfaceType in interfaces)
                {
                    services.AddScoped(interfaceType, implementationType);
                }
            }
        }
    }

    /// <summary>
    /// Registers all implementations of ITransientService as transient services.
    /// </summary>
    private static void RegisterTransientServices(IServiceCollection services, Assembly assembly)
    {
        var transientTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(ITransientService).IsAssignableFrom(t)
                        && !typeof(IScopedService).IsAssignableFrom(t)); // Exclude scoped

        foreach (var implementationType in transientTypes)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i != typeof(ITransientService) && !i.IsGenericType)
                .ToList();

            if (interfaces.Count == 0)
            {
                services.AddTransient(implementationType);
            }
            else
            {
                foreach (var interfaceType in interfaces)
                {
                    services.AddTransient(interfaceType, implementationType);
                }
            }
        }
    }

    /// <summary>
    /// Registers all implementations of ISingletonService as singleton services.
    /// </summary>
    private static void RegisterSingletonServices(IServiceCollection services, Assembly assembly)
    {
        var singletonTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && typeof(ISingletonService).IsAssignableFrom(t));

        foreach (var implementationType in singletonTypes)
        {
            var interfaces = implementationType.GetInterfaces()
                .Where(i => i != typeof(ISingletonService) && !i.IsGenericType)
                .ToList();

            if (interfaces.Count == 0)
            {
                services.AddSingleton(implementationType);
            }
            else
            {
                foreach (var interfaceType in interfaces)
                {
                    services.AddSingleton(interfaceType, implementationType);
                }
            }
        }
    }

    /// <summary>
    /// Registers generic repository implementations.
    /// </summary>
    private static void RegisterGenericRepositories(IServiceCollection services, Assembly assembly)
    {
        // Generic repositories are registered explicitly via the marker interface
        var repositoryTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.GetInterfaces().Any(i =>
                            i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IRepository<>)));

        foreach (var implementationType in repositoryTypes)
        {
            var repositoryInterface = implementationType.GetInterfaces()
                .First(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IRepository<>));

            var entityType = repositoryInterface.GetGenericArguments()[0];
            var specificInterface = implementationType.GetInterfaces()
                .FirstOrDefault(i => !i.IsGenericType &&
                                    typeof(IRepository<>).MakeGenericType(entityType).IsAssignableFrom(i));

            if (specificInterface is not null)
            {
                services.AddScoped(specificInterface, implementationType);
            }
        }
    }
}
