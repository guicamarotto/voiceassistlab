using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using Microsoft.Extensions.Options;
using VoiceAssistLab.Core.Llm;

namespace VoiceAssistLab.Infra.Llm;

public sealed class GroqLlmClient : ILlmClient
{
    private readonly ChatClient _chatClient;
    private readonly GroqOptions _options;

    public GroqLlmClient(IOptions<GroqOptions> options)
    {
        _options = options.Value;
        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(_options.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_options.BaseUrl) });
        _chatClient = openAiClient.GetChatClient(_options.Model);
    }

    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        var messages = BuildMessages(request);
        var options = BuildOptions(request);

        var result = await _chatClient.CompleteChatAsync(messages, options, ct);
        return MapResponse(result.Value);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = BuildMessages(request);
        var options = BuildOptions(request);

        await foreach (var update in _chatClient.CompleteChatStreamingAsync(messages, options, ct))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                    yield return part.Text;
            }
        }
    }

    private static List<ChatMessage> BuildMessages(LlmRequest request)
    {
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(request.SystemPrompt)
        };

        foreach (var m in request.Messages)
        {
            if (m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                messages.Add(new AssistantChatMessage(m.Content));
            else
                messages.Add(new UserChatMessage(m.Content));
        }

        return messages;
    }

    private static ChatCompletionOptions BuildOptions(LlmRequest request)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
            MaxOutputTokenCount = request.MaxTokens
        };

        if (request.Tools is { Count: > 0 })
        {
            foreach (var tool in request.Tools)
            {
                options.Tools.Add(ChatTool.CreateFunctionTool(
                    functionName: tool.Name,
                    functionDescription: tool.Description,
                    functionParameters: BinaryData.FromString(tool.Schema.GetRawText())));
            }
        }

        return options;
    }

    private static LlmResponse MapResponse(ChatCompletion completion)
    {
        var content = completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;

        var toolCalls = completion.ToolCalls
            .Select(tc =>
            {
                var args = JsonSerializer.Deserialize<JsonElement>(
                    tc.FunctionArguments.ToArray());
                return new LlmToolCall(tc.FunctionName, args);
            })
            .ToList();

        var usage = new LlmUsage(
            completion.Usage.InputTokenCount,
            completion.Usage.OutputTokenCount);

        return new LlmResponse(content, toolCalls.Count > 0 ? toolCalls : null, usage);
    }
}
