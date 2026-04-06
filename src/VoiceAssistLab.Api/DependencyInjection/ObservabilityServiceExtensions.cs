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
        var otlpEndpoint = configuration["Otel:Endpoint"];

        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("VoiceAssistLab"))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                // Only export when an explicit endpoint is configured — avoids
                // blocking startup when Seq / collector is not running locally.
                if (!string.IsNullOrEmpty(otlpEndpoint))
                    tracing.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(otlpEndpoint);
                        o.TimeoutMilliseconds = 2_000;
                    });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(MeterName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                    metrics.AddOtlpExporter(o =>
                    {
                        o.Endpoint = new Uri(otlpEndpoint);
                        o.TimeoutMilliseconds = 2_000;
                    });
            });

        return services;
    }
}
