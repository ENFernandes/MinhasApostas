using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace SportsBetting.DataCollector.Api.Extensions;

/// <summary>
/// Extension methods for adding HTTP resilience policies (retry + circuit breaker) to Refit clients.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Adiciona um pipeline de resiliência ao cliente HTTP:
    /// - Retry: 3 tentativas com backoff exponencial (2s, 4s, 8s) e jitter
    /// - Circuit Breaker: abre após 50% de falhas em janela de 30s (mínimo 5 requests), fica aberto 60s
    /// </summary>
    /// <param name="builder">O builder do HTTP client.</param>
    /// <param name="clientName">Nome do cliente para identificação nos logs (ex: "FootballData").</param>
    public static IHttpClientBuilder AddSportsBettingResilience(
        this IHttpClientBuilder builder, string clientName)
    {
        builder.AddResilienceHandler("sports-betting", (pipeline, context) =>
        {
            var loggerFactory = context.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger($"Resilience.{clientName}");

            // Retry: 3 tentativas com backoff exponencial e jitter para evitar thundering herd
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Exception is HttpRequestException ||
                    (args.Outcome.Result?.StatusCode is
                        System.Net.HttpStatusCode.TooManyRequests or
                        System.Net.HttpStatusCode.ServiceUnavailable or
                        System.Net.HttpStatusCode.GatewayTimeout or
                        System.Net.HttpStatusCode.RequestTimeout)),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "[{Client}] Retry {Attempt}/{Max} para {Url}. Motivo: {Reason}",
                        clientName,
                        args.AttemptNumber + 1,
                        3,
                        args.Outcome.Result?.RequestMessage?.RequestUri?.AbsolutePath ?? "unknown",
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                }
            });

            // Circuit Breaker: abre quando 50% dos requests falham numa janela de 30s
            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
                OnOpened = args =>
                {
                    logger.LogError(
                        "[{Client}] Circuit Breaker ABERTO por 60s. Motivo: {Reason}",
                        clientName,
                        args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation(
                        "[{Client}] Circuit Breaker FECHADO — requests retomados.",
                        clientName);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    logger.LogInformation(
                        "[{Client}] Circuit Breaker SEMI-ABERTO — testando recuperação.",
                        clientName);
                    return ValueTask.CompletedTask;
                }
            });
        });

        return builder;
    }
}
