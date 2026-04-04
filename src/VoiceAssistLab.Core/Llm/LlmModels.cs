using System.Text.Json;

namespace VoiceAssistLab.Core.Llm;

public record LlmRequest(
    string SystemPrompt,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmTool>? Tools = null,
    float Temperature = 0.3f,
    int MaxTokens = 1024
);

public record LlmResponse(
    string Content,
    IReadOnlyList<LlmToolCall>? ToolCalls,
    LlmUsage Usage
);

public record LlmMessage(string Role, string Content);

public record LlmUsage(int InputTokens, int OutputTokens);

public record LlmTool(string Name, string Description, JsonElement Schema);

public record LlmToolCall(string ToolName, JsonElement Arguments);
