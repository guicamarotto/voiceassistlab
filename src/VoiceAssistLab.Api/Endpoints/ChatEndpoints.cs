using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using VoiceAssistLab.Core.Chat;
using VoiceAssistLab.Core.Guardrails;

namespace VoiceAssistLab.Api.Endpoints;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat");

        group.MapPost("/", HandleChatAsync)
            .WithName("Chat")
            .WithSummary("Send a message and receive a streamed response via SSE")
            .Produces(200, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .RequireRateLimiting("chat");

        return app;
    }

    private static async Task HandleChatAsync(
        HttpContext ctx,
        IChatOrchestrator orchestrator,
        IValidator<ChatRequest> validator,
        IInputGuardrail inputGuardrail,
        IOutputGuardrail outputGuardrail,
        CancellationToken ct)
    {
        var body = await ctx.Request.ReadFromJsonAsync<ChatRequestDto>(ct);
        if (body is null)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new { error = "Request body is required." }, ct);
            return;
        }

        var request = new ChatRequest(
            body.Message,
            body.SessionId ?? Guid.NewGuid().ToString(),
            body.History?.Select(h => new ChatTurn(h.UserMessage, h.AssistantMessage)).ToList());

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "Validation failed",
                details = validation.Errors.Select(e => e.ErrorMessage)
            }, ct);
            return;
        }

        // Input guardrail
        var inputCheck = await inputGuardrail.ValidateAsync(body.Message, ct);
        if (!inputCheck.IsSuccess)
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            var msg = JsonSerializer.Serialize(new { type = "token", content = inputCheck.Error });
            await ctx.Response.WriteAsync($"data: {msg}\n\ndata: [DONE]\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
            return;
        }

        ctx.Response.Headers.ContentType = "text/event-stream";
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        ctx.Response.Headers.Connection = "keep-alive";

        // Stream tokens, accumulating for output guardrail
        var accumulated = new System.Text.StringBuilder();

        await foreach (var token in orchestrator.StreamAsync(request, ct))
        {
            accumulated.Append(token);
            var data = JsonSerializer.Serialize(new { type = "token", content = token });
            await ctx.Response.WriteAsync($"data: {data}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }

        // Output guardrail (non-blocking — only log if it fails; response already sent)
        var outputCheck = await outputGuardrail.ValidateAsync(accumulated.ToString(), body.Message, ct);
        if (!outputCheck.IsSuccess)
        {
            // The stream is already partially sent; append a correction notice
            var correction = JsonSerializer.Serialize(new { type = "token", content = " " + outputCheck.Error });
            await ctx.Response.WriteAsync($"data: {correction}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }

        await ctx.Response.WriteAsync("data: [DONE]\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    // DTOs for the HTTP layer only
    private sealed record ChatRequestDto(
        string Message,
        string? SessionId,
        IReadOnlyList<ChatTurnDto>? History);

    private sealed record ChatTurnDto(string UserMessage, string AssistantMessage);
}
