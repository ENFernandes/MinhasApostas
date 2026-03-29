namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Marker interface for repository services.
/// Implementations are automatically registered as Scoped services.
/// </summary>
/// <typeparam name="T">The entity type this repository handles.</typeparam>
public interface IRepository<T> : IScopedService where T : class
{
}
