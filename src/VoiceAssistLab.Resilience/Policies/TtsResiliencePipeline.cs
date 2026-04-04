using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace VoiceAssistLab.Resilience.Policies;

public static class TtsResiliencePipeline
{
    /// <summary>
    /// Timeout 5s for first byte. No retry — TTS failures fall back to silent audio in KokoroTtsClient.
    /// </summary>
    public static IHttpResiliencePipelineBuilder AddTtsResilienceHandler(
        this IHttpClientBuilder builder)
    {
        return builder.AddResilienceHandler("tts", pipeline =>
        {
            pipeline.AddTimeout(TimeSpan.FromSeconds(5));
        });
    }
}
