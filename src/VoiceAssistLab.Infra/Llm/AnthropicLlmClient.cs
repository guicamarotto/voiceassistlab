using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using Microsoft.Extensions.Options;
using VoiceAssistLab.Core.Llm;

namespace VoiceAssistLab.Infra.Llm;

public sealed class AnthropicLlmClient : ILlmClient
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;

    public AnthropicLlmClient(AnthropicClient client, IOptions<AnthropicOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<LlmResponse> SendAsync(LlmRequest request, CancellationToken ct = default)
    {
        var parameters = BuildParameters(request, stream: false);
        var response = await _client.Messages.GetClaudeMessageAsync(parameters, ct);
        return MapResponse(response);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        LlmRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var parameters = BuildParameters(request, stream: true);

        // StreamClaudeMessageAsync yields MessageResponse deltas
        await foreach (var response in _client.Messages.StreamClaudeMessageAsync(parameters, ct))
        {
            var text = response.Delta?.Text;
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    private MessageParameters BuildParameters(LlmRequest request, bool stream) => new()
    {
        Model = _options.Model,
        MaxTokens = request.MaxTokens,
        System = [new SystemMessage(request.SystemPrompt)],
        Messages = BuildMessages(request),
        Tools = MapTools(request.Tools),
        Stream = stream,
        Temperature = (decimal)request.Temperature
    };

    private static List<Message> BuildMessages(LlmRequest request)
        => request.Messages.Select(m => new Message
        {
            Role = m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? RoleType.Assistant
                : RoleType.User,
            Content = [new TextContent { Text = m.Content }]
        }).ToList();

    private static List<Anthropic.SDK.Common.Tool>? MapTools(IReadOnlyList<LlmTool>? tools)
    {
        if (tools is null or { Count: 0 }) return null;

        return tools.Select(t =>
        {
            // Function constructor takes JsonNode, convert from JsonElement
            var schemaNode = JsonNode.Parse(t.Schema.GetRawText());
            return new Anthropic.SDK.Common.Tool(new Function(t.Name, t.Description, schemaNode));
        }).ToList();
    }

    private static LlmResponse MapResponse(MessageResponse response)
    {
        var content = string.Concat(
            response.Content.OfType<TextContent>().Select(t => t.Text ?? string.Empty));

        var toolCalls = response.Content
            .OfType<ToolUseContent>()
            .Select(tu =>
            {
                // tu.Input is JsonNode, serialize then deserialize to JsonElement
                var json = tu.Input?.ToJsonString() ?? "{}";
                var element = JsonSerializer.Deserialize<JsonElement>(json);
                return new LlmToolCall(tu.Name, element);
            })
            .ToList();

        var usage = new LlmUsage(
            response.Usage?.InputTokens ?? 0,
            response.Usage?.OutputTokens ?? 0);

        return new LlmResponse(content, toolCalls.Count > 0 ? toolCalls : null, usage);
    }
}
