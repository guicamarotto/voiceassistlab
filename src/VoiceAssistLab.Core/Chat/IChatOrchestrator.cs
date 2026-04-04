namespace VoiceAssistLab.Core.Chat;

public interface IChatOrchestrator
{
    Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(ChatRequest request, CancellationToken ct = default);
}
