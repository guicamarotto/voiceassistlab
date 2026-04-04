using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using VoiceAssistLab.Core.Llm;
using VoiceAssistLab.Core.Tools;

namespace VoiceAssistLab.Core.Chat;

public sealed class ChatOrchestrator(
    ILlmClient llmClient,
    ToolRegistry toolRegistry,
    ILogger<ChatOrchestrator> logger) : IChatOrchestrator
{
    private const int MaxToolCallIterations = 3;
    private const int MaxHistoryTurns = 10;

    internal static readonly ActivitySource ActivitySource = new("VoiceAssistLab.Chat");
    internal static readonly Meter Meter = new("VoiceAssistLab");

    private static readonly Counter<long> TokenCounter =
        Meter.CreateCounter<long>("llm.tokens", "tokens", "Total LLM tokens consumed");
    private static readonly Histogram<double> TtftHistogram =
        Meter.CreateHistogram<double>("llm.ttft_ms", "ms", "Time-to-first-token in milliseconds");
    private static readonly Counter<long> ToolCallCounter =
        Meter.CreateCounter<long>("llm.tool_calls", "calls", "Number of tool calls dispatched");

    public async Task<ChatResponse> SendAsync(ChatRequest request, CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("llm.send");
        activity?.SetTag("session_id", request.SessionId);

        var systemPrompt = LoadSystemPrompt();
        var messages = BuildMessages(request);
        var tools = BuildTools();
        var toolCallsMade = new List<ToolCallSummary>();

        for (var iteration = 0; iteration < MaxToolCallIterations; iteration++)
        {
            var llmRequest = new LlmRequest(systemPrompt, messages, tools);
            var response = await llmClient.SendAsync(llmRequest, ct);

            TokenCounter.Add(response.Usage.InputTokens + response.Usage.OutputTokens);

            if (response.ToolCalls is null or { Count: 0 })
                return new ChatResponse(response.Content, request.SessionId ?? Guid.NewGuid().ToString(), toolCallsMade);

            // Execute tool calls and append results
            messages = [.. messages, new LlmMessage("assistant", response.Content)];

            foreach (var toolCall in response.ToolCalls)
            {
                using var toolActivity = ActivitySource.StartActivity("llm.tool_call");
                toolActivity?.SetTag("tool_name", toolCall.ToolName);
                ToolCallCounter.Add(1);

                logger.LogInformation("Executing tool {ToolName}", toolCall.ToolName);
                var result = await toolRegistry.ExecuteAsync(toolCall.ToolName, toolCall.Arguments, ct);
                toolCallsMade.Add(new ToolCallSummary(toolCall.ToolName, result));

                messages = [.. messages, new LlmMessage("user", $"[tool_result:{toolCall.ToolName}]\n{result}")];
            }
        }

        logger.LogWarning("Max tool call iterations ({Max}) reached for session {SessionId}",
            MaxToolCallIterations, request.SessionId);
        return new ChatResponse(
            "Desculpe, não consegui processar sua solicitação no momento. Por favor, tente novamente.",
            request.SessionId ?? Guid.NewGuid().ToString(),
            toolCallsMade);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("llm.stream");
        activity?.SetTag("session_id", request.SessionId);

        var systemPrompt = LoadSystemPrompt();
        var messages = BuildMessages(request);
        var tools = BuildTools();
        var sw = Stopwatch.StartNew();
        var firstToken = true;

        for (var iteration = 0; iteration < MaxToolCallIterations; iteration++)
        {
            var llmRequest = new LlmRequest(systemPrompt, messages, tools);

            // Peek: do a non-streaming call to check for tool calls
            var peekResponse = await llmClient.SendAsync(llmRequest, ct);
            TokenCounter.Add(peekResponse.Usage.InputTokens + peekResponse.Usage.OutputTokens);

            if (peekResponse.ToolCalls is null or { Count: 0 })
            {
                // No tool calls — stream the response
                var streamRequest = new LlmRequest(systemPrompt, messages, tools);
                await foreach (var token in llmClient.StreamAsync(streamRequest, ct))
                {
                    if (firstToken)
                    {
                        TtftHistogram.Record(sw.Elapsed.TotalMilliseconds);
                        firstToken = false;
                    }
                    yield return token;
                }
                yield break;
            }

            // Execute tool calls
            messages = [.. messages, new LlmMessage("assistant", peekResponse.Content)];
            foreach (var toolCall in peekResponse.ToolCalls)
            {
                using var toolActivity = ActivitySource.StartActivity("llm.tool_call");
                toolActivity?.SetTag("tool_name", toolCall.ToolName);
                ToolCallCounter.Add(1);

                var result = await toolRegistry.ExecuteAsync(toolCall.ToolName, toolCall.Arguments, ct);
                messages = [.. messages, new LlmMessage("user", $"[tool_result:{toolCall.ToolName}]\n{result}")];
            }
        }

        yield return "Desculpe, não consegui processar sua solicitação no momento. Por favor, tente novamente.";
    }

    private static IReadOnlyList<LlmMessage> BuildMessages(ChatRequest request)
    {
        var messages = new List<LlmMessage>();

        if (request.History is { Count: > 0 })
        {
            var recent = request.History.TakeLast(MaxHistoryTurns);
            foreach (var turn in recent)
            {
                messages.Add(new LlmMessage("user", turn.UserMessage));
                messages.Add(new LlmMessage("assistant", turn.AssistantMessage));
            }
        }

        messages.Add(new LlmMessage("user", request.Message));
        return messages;
    }

    private IReadOnlyList<LlmTool> BuildTools()
        => toolRegistry.All
            .Select(t => new LlmTool(t.Name, t.Description, t.Schema))
            .ToList();

    private static string LoadSystemPrompt()
    {
        var assembly = typeof(ChatOrchestrator).Assembly;
        var resourceName = "VoiceAssistLab.Core.Prompts.system-prompt.txt";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
