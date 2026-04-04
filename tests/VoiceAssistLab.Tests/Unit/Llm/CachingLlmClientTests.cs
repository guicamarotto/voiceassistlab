using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using VoiceAssistLab.Core.Llm;
using VoiceAssistLab.Infra.Cache;

namespace VoiceAssistLab.Tests.Unit.Llm;

public sealed class CachingLlmClientTests : IDisposable
{
    private readonly ILlmClient _inner = Substitute.For<ILlmClient>();
    private readonly IMemoryCache _cache;
    private readonly CachingLlmClient _sut;

    public CachingLlmClientTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new CachingLlmClient(_inner, _cache);
    }

    private static LlmRequest MakeRequest(string message = "What is your return policy?")
        => new(
            SystemPrompt: "You are a support agent.",
            Messages: [new LlmMessage("user", message)]);

    private static LlmResponse MakeResponse(string content = "Our return policy is 30 days.")
        => new(content, null, new LlmUsage(10, 20));

    [Fact]
    public async Task SendAsync_FirstCall_InvokesInnerClient()
    {
        var request = MakeRequest();
        var expected = MakeResponse();
        _inner.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
              .Returns(expected);

        var result = await _sut.SendAsync(request);

        result.Should().Be(expected);
        await _inner.Received(1).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SecondIdenticalCall_ReturnsCachedResult()
    {
        var request = MakeRequest();
        var expected = MakeResponse();
        _inner.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
              .Returns(expected);

        await _sut.SendAsync(request);
        var result = await _sut.SendAsync(request);

        result.Should().Be(expected);
        await _inner.Received(1).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_DifferentMessages_CallsInnerClientTwice()
    {
        _inner.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
              .Returns(MakeResponse());

        await _sut.SendAsync(MakeRequest("message 1"));
        await _sut.SendAsync(MakeRequest("message 2"));

        await _inner.Received(2).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ResponseWithToolCalls_IsNotCached()
    {
        var request = MakeRequest();
        var toolCallResponse = new LlmResponse(
            "I need to check your order.",
            [new LlmToolCall("get_order_status", JsonSerializer.SerializeToElement(new { order_id = "ORD-001" }))],
            new LlmUsage(10, 20));
        _inner.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
              .Returns(toolCallResponse);

        await _sut.SendAsync(request);
        await _sut.SendAsync(request);

        await _inner.Received(2).SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void StreamAsync_AlwaysDelegatesToInner()
    {
        var request = MakeRequest();
        _inner.StreamAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
              .Returns(AsyncEnumerable.Empty<string>());

        _ = _sut.StreamAsync(request);

        _inner.Received(1).StreamAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _cache.Dispose();
}

file static class AsyncEnumerable
{
    public static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
