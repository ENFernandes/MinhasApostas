using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SportsBetting.DataCollector.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace SportsBetting.DataCollector.Infrastructure.Services;

/// <summary>
/// RabbitMQ implementation of the message queue publisher.
/// </summary>
public class RabbitMqPublisher : IMessageQueuePublisher, ISingletonService, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private bool _disposed;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        var host = configuration["RabbitMQ:Host"] ?? "localhost";
        var user = configuration["RabbitMQ:User"] ?? "guest";
        var password = configuration["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = password,
            AutomaticRecoveryEnabled = true
        };

        try
        {
            // Create connection and channel synchronously for constructor
            _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
            _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

            // Declare exchanges
            _channel.ExchangeDeclareAsync("sports.events", ExchangeType.Topic, durable: true).GetAwaiter().GetResult();
            _channel.ExchangeDeclareAsync("analysis.results", ExchangeType.Topic, durable: true).GetAwaiter().GetResult();

            _logger.LogInformation("RabbitMQ publisher connected to {Host}", host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to RabbitMQ");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(string exchange, string routingKey, object message, CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RabbitMqPublisher));

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var props = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        };

        // Use the async method for publishing
        await _channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogDebug("Published message to {Exchange}:{RoutingKey}", exchange, routingKey);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }
}
