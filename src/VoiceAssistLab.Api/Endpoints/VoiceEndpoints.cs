using VoiceAssistLab.Voice.WebSocket;

namespace VoiceAssistLab.Api.Endpoints;

public static class VoiceEndpoints
{
    public static IEndpointRouteBuilder MapVoiceEndpoints(this IEndpointRouteBuilder app)
    {
        app.Map("/ws/voice", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("WebSocket connection required.");
                return;
            }

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var handler = ctx.RequestServices.GetRequiredService<VoiceWebSocketHandler>();
            await handler.HandleAsync(ws, ctx.RequestAborted);
        });

        return app;
    }
}
