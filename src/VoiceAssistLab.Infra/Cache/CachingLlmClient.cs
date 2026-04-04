using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using VoiceAssistLab.Core.Llm;

namespace VoiceAssistLab.Infra.Cache;

public sealed class CachingLlmClient(ILlmClient inner, IMemoryCache cache) : ILlmClient
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        // Don't cache if there's active tool call history (context-sensitive)
        if (HasToolCallHistory(request))
            return await inner.SendAsync(request, ct);

        var key = ComputeCacheKey(request);

        if (cache.TryGetValue(key, out LlmResponse? cached) && cached is not null)
            return cached;

        var response = await inner.SendAsync(request, ct);

        // Only cache clean text responses (no tool calls needed)
        if (response.ToolCalls is null or { Count: 0 })
            cache.Set(key, response, Ttl);

        return response;
    }

    public IAsyncEnumerable<string> StreamAsync(LlmRequest request, CancellationToken ct = default)
        => inner.StreamAsync(request, ct); // Streaming is not cached

    private static bool HasToolCallHistory(LlmRequest request)
        => request.Messages.Any(m => m.Content.Contains("tool_result", StringComparison.OrdinalIgnoreCase));

    private static string ComputeCacheKey(LlmRequest request)
    {
        var payload = JsonSerializer.Serialize(new
        {
            request.SystemPrompt,
            messages = request.Messages,
            tools = request.Tools?.Select(t => t.Name)
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return $"llm:{Convert.ToHexString(hash)}";
    }
}
