namespace SportsBetting.DataCollector.Core.Interfaces;

/// <summary>
/// Interface for publishing messages to the message queue.
/// </summary>
public interface IMessageQueuePublisher
{
    /// <summary>
    /// Publishes a message to the specified exchange with routing key.
    /// </summary>
    Task PublishAsync(string exchange, string routingKey, object message, CancellationToken cancellationToken = default);
}
