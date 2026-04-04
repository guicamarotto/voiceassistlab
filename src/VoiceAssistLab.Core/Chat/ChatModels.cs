namespace VoiceAssistLab.Core.Chat;

public record ChatRequest(
    string Message,
    string? SessionId = null,
    IReadOnlyList<ChatTurn>? History = null
);

public record ChatResponse(
    string Content,
    string SessionId,
    IReadOnlyList<ToolCallSummary>? ToolCallsMade = null
);

public record ChatTurn(string UserMessage, string AssistantMessage);

public record ToolCallSummary(string ToolName, string Result);
