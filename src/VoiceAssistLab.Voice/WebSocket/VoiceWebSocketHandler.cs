using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VoiceAssistLab.Voice.Pipeline;

namespace VoiceAssistLab.Voice.WebSocket;

/// <summary>
/// Manages the WebSocket lifecycle for the voice pipeline.
/// - Receives PCM binary frames from the browser
/// - Sends audio chunks (MP3) back as binary frames
/// - Sends transcript/status messages as text frames (JSON)
/// </summary>
public sealed class VoiceWebSocketHandler(
    VoicePipeline pipeline,
    ILogger<VoiceWebSocketHandler> logger)
{
    public async Task HandleAsync(System.Net.WebSockets.WebSocket ws, CancellationToken ct)
    {
        logger.LogInformation("Voice WebSocket connection established");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sessionId = Guid.NewGuid().ToString("N")[..8];

        var pipelineTask = pipeline.RunAsync(sessionId, cts.Token);
        var receiveTask = ReceiveLoopAsync(ws, cts.Token);
        var sendTask = SendLoopAsync(ws, cts.Token);
        var transcriptTask = TranscriptLoopAsync(ws, cts.Token);

        try
        {
            await Task.WhenAny(receiveTask, pipelineTask);
        }
        finally
        {
            await cts.CancelAsync();
            await Task.WhenAll(sendTask, transcriptTask);
        }

        logger.LogInformation("Voice WebSocket session {SessionId} closed", sessionId);
    }

    private async Task ReceiveLoopAsync(System.Net.WebSockets.WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", ct);
                pipeline.AudioInput.Complete();
                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                var chunk = new byte[result.Count];
                Array.Copy(buffer, chunk, result.Count);
                await pipeline.AudioInput.WriteAsync(chunk, ct);
            }
        }
    }

    private async Task SendLoopAsync(System.Net.WebSockets.WebSocket ws, CancellationToken ct)
    {
        await foreach (var audioChunk in pipeline.AudioOutput.ReadAllAsync(ct))
        {
            if (ws.State != WebSocketState.Open) break;

            await ws.SendAsync(
                new ArraySegment<byte>(audioChunk),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                ct);
        }
    }

    private async Task TranscriptLoopAsync(System.Net.WebSockets.WebSocket ws, CancellationToken ct)
    {
        await foreach (var transcript in pipeline.TranscriptOutput.ReadAllAsync(ct))
        {
            if (ws.State != WebSocketState.Open) break;

            var message = JsonSerializer.Serialize(new { type = "transcript", text = transcript });
            var bytes = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                ct);
        }
    }
}
