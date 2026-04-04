using VoiceAssistLab.Resilience.Policies;
using VoiceAssistLab.Voice.Asr;
using VoiceAssistLab.Voice.Pipeline;
using VoiceAssistLab.Voice.Tts;
using VoiceAssistLab.Voice.WebSocket;

namespace VoiceAssistLab.Api.DependencyInjection;

public static class VoiceServiceExtensions
{
    public static IServiceCollection AddVoiceServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ASR
        services.Configure<WhisperOptions>(configuration.GetSection("Whisper"));
        services.AddHttpClient<IAsrClient, WhisperAsrClient>(http =>
        {
            var baseUrl = configuration["Whisper:BaseUrl"] ?? "http://localhost:8081";
            http.BaseAddress = new Uri(baseUrl);
        })
        .AddAsrResilienceHandler();

        // TTS
        services.Configure<KokoroOptions>(configuration.GetSection("Kokoro"));
        services.AddHttpClient<ITtsClient, KokoroTtsClient>(http =>
        {
            var baseUrl = configuration["Kokoro:BaseUrl"] ?? "http://localhost:3000";
            http.BaseAddress = new Uri(baseUrl);
        })
        .AddTtsResilienceHandler();

        // Pipeline (scoped per WebSocket connection)
        services.AddScoped<VoicePipeline>();
        services.AddScoped<VoiceWebSocketHandler>();

        return services;
    }
}
