using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using VoiceAssistLab.Core.Chat;
using VoiceAssistLab.Core.Llm;
using VoiceAssistLab.Core.Tools;
using VoiceAssistLab.Infra.MockData;

namespace VoiceAssistLab.Tests.Unit.Chat;

public sealed class ChatOrchestratorTests
{
    private readonly ILlmClient _llmClient = Substitute.For<ILlmClient>();
    private readonly ToolRegistry _toolRegistry;
    private readonly ChatOrchestrator _sut;

    public ChatOrchestratorTests()
    {
        var repo = new MockDataRepository();
        _toolRegistry = new ToolRegistry([
            new GetOrderStatusTool(repo),
            new GetProductInfoTool(repo),
            new GetReturnPolicyTool(repo)
        ]);
        _sut = new ChatOrchestrator(_llmClient, _toolRegistry, NullLogger<ChatOrchestrator>.Instance);
    }

    [Fact]
    public async Task SendAsync_SimpleTextResponse_ReturnsContentDirectly()
    {
        var request = new ChatRequest("What is your return policy?");
        var expectedResponse = new LlmResponse("You have 30 days to return items.", null, new LlmUsage(10, 20));
        _llmClient.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                  .Returns(expectedResponse);

        var result = await _sut.SendAsync(request);

        result.Content.Should().Be("You have 30 days to return items.");
        result.ToolCallsMade.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_SingleToolCall_ExecutesToolAndReturnsFollowUp()
    {
        var request = new ChatRequest("What is the status of order ORD-001?");
        var toolCallArgs = JsonSerializer.SerializeToElement(new { order_id = "ORD-001" });

        // First call returns a tool call
        var toolCallResponse = new LlmResponse(
            "",
            [new LlmToolCall("get_order_status", toolCallArgs)],
            new LlmUsage(10, 5));

        // Second call (after tool result) returns the final answer
        var finalResponse = new LlmResponse(
            "Order ORD-001 has been delivered.",
            null,
            new LlmUsage(20, 15));

        _llmClient.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                  .Returns(toolCallResponse, finalResponse);

        var result = await _sut.SendAsync(request);

        result.Content.Should().Be("Order ORD-001 has been delivered.");
        result.ToolCallsMade.Should().HaveCount(1);
        result.ToolCallsMade![0].ToolName.Should().Be("get_order_status");
    }

    [Fact]
    public async Task SendAsync_ExceedsMaxToolCallIterations_ReturnsFallbackMessage()
    {
        var request = new ChatRequest("Trigger max tool calls");
        var toolCallArgs = JsonSerializer.SerializeToElement(new { order_id = "ORD-001" });
        var toolCallResponse = new LlmResponse(
            "",
            [new LlmToolCall("get_order_status", toolCallArgs)],
            new LlmUsage(10, 5));

        // Always returns a tool call — will hit the 3-iteration limit
        _llmClient.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                  .Returns(toolCallResponse);

        var result = await _sut.SendAsync(request);

        result.Content.Should().Contain("não consegui processar");
    }

    [Fact]
    public async Task SendAsync_WithHistory_IncludesHistoryInMessages()
    {
        var history = new List<ChatTurn>
        {
            new("Hello", "Hi there!")
        };
        var request = new ChatRequest("Follow up question", History: history);
        var expectedResponse = new LlmResponse("Follow up answer.", null, new LlmUsage(15, 10));
        _llmClient.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
                  .Returns(expectedResponse);

        await _sut.SendAsync(request);

        await _llmClient.Received(1).SendAsync(
            Arg.Is<LlmRequest>(r => r.Messages.Count == 3), // history user + history assistant + current user
            Arg.Any<CancellationToken>());
    }
}
