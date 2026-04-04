using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace VoiceAssistLab.Api.DependencyInjection;

public static class ObservabilityServiceExtensions
{
    // Must match the names used in ChatOrchestrator
    private const string ActivitySourceName = "VoiceAssistLab.Chat";
    private const string MeterName = "VoiceAssistLab";

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Otel:Endpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("VoiceAssistLab"))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddMeter(MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }
}
