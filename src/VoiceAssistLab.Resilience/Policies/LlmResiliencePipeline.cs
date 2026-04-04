using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace VoiceAssistLab.Resilience.Policies;

public static class LlmResiliencePipeline
{
    /// <summary>
    /// Adds retry (3×, exp. backoff 1→4s), circuit breaker (5 failures / 30s break), timeout 10s.
    /// Apply via IHttpClientBuilder.AddResilienceHandler("llm", ...).
    /// </summary>
    public static IHttpResiliencePipelineBuilder AddLlmResilienceHandler(
        this IHttpClientBuilder builder)
    {
        return builder.AddResilienceHandler("llm", pipeline =>
        {
            pipeline.AddTimeout(TimeSpan.FromSeconds(10));

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = Polly.DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode is
                        System.Net.HttpStatusCode.TooManyRequests or
                        System.Net.HttpStatusCode.InternalServerError or
                        System.Net.HttpStatusCode.BadGateway or
                        System.Net.HttpStatusCode.ServiceUnavailable or
                        System.Net.HttpStatusCode.GatewayTimeout
                    || args.Outcome.Exception is not null),
            });

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 5,
                FailureRatio = 0.5,
                BreakDuration = TimeSpan.FromSeconds(30),
            });
        });
    }
}
