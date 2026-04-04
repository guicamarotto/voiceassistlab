namespace VoiceAssistLab.Core.Llm;

public interface ILlmClient
{
    /// <summary>Sends a request and returns the complete response.</summary>
    Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>Sends a request and streams tokens one by one.</summary>
    IAsyncEnumerable<string> StreamAsync(LlmRequest request, CancellationToken ct = default);
}
