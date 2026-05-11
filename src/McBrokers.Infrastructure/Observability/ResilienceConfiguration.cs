using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace McBrokers.Infrastructure.Observability;

/// <summary>
/// Pipeline de resilience compartido para los HttpClients de adapters de aseguradora.
/// Estrategia: 3 reintentos exponenciales (1s, 2s, 4s) sobre fallos transitorios (HTTP 5xx,
/// 408 Request Timeout, errores de red, HttpRequestException, TaskCanceledException).
/// Circuit breaker: 5 fallos en ventana de 30s abren el circuito 60s.
/// </summary>
public static class ResilienceConfiguration
{
    public static IHttpClientBuilder AddMcBrokersResilience(this IHttpClientBuilder builder)
    {
        builder.AddResilienceHandler("mcbrokers-default", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(60),
            });

            pipeline.AddTimeout(TimeSpan.FromSeconds(60));
        });

        return builder;
    }
}
