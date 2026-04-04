using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace VoiceAssistLab.Resilience.Policies;

public static class AsrResiliencePipeline
{
    /// <summary>
    /// Retry 2×, 500ms fixed delay, timeout 5s.
    /// </summary>
    public static IHttpResiliencePipelineBuilder AddAsrResilienceHandler(
        this IHttpClientBuilder builder)
    {
        return builder.AddResilienceHandler("asr", pipeline =>
        {
            pipeline.AddTimeout(TimeSpan.FromSeconds(5));

            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = Polly.DelayBackoffType.Constant,
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Result?.StatusCode is
                        System.Net.HttpStatusCode.InternalServerError or
                        System.Net.HttpStatusCode.ServiceUnavailable
                    || args.Outcome.Exception is not null),
            });
        });
    }
}
