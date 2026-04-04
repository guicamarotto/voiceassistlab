using Microsoft.Extensions.Options;
using VoiceAssistLab.Infra.Llm;

namespace VoiceAssistLab.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", (IConfiguration config) => Results.Ok(new
        {
            status = "ok",
            provider = config["Llm:Provider"] ?? "groq",
            timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("Health")
        .WithSummary("Health check");

        return app;
    }
}
