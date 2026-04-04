using System.Net;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VoiceAssistLab.Core.Chat;
using VoiceAssistLab.Core.Llm;

namespace VoiceAssistLab.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/chat using WebApplicationFactory.
/// The real LLM client is replaced with an NSubstitute mock so no network calls are made.
/// </summary>
public sealed class ChatEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ChatEndpointsTests(WebApplicationFactory<Program> factory)
    {
        var mockLlm = Substitute.For<ILlmClient>();

        // Default: return a simple text response with no tool calls
        mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("Olá! Como posso ajudar?", null, new LlmUsage(10, 5)));

        mockLlm.StreamAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable("Olá", "! Como", " posso ajudar?"));

        _client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                // Remove the real ILlmClient registration and replace with mock
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILlmClient));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton(mockLlm);
            }))
            .CreateClient();
    }

    [Fact]
    public async Task PostChat_WithValidMessage_ReturnsSseStream()
    {
        var response = await _client.PostAsJsonAsync("/api/chat",
            new { message = "Qual o status do meu pedido?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("data:");
        body.Should().Contain("[DONE]");
    }

    [Fact]
    public async Task PostChat_WithEmptyMessage_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/chat", new { message = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("error");
    }

    [Fact]
    public async Task PostChat_WithMessageExceeding500Chars_Returns400()
    {
        var longMessage = new string('a', 501);
        var response = await _client.PostAsJsonAsync("/api/chat", new { message = longMessage });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostChat_WithNullBody_Returns400()
    {
        var content = new StringContent("null", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/api/chat", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostChat_SseResponse_ContainsTokenEvents()
    {
        var response = await _client.PostAsJsonAsync("/api/chat",
            new { message = "Olá" });

        var body = await response.Content.ReadAsStringAsync();

        // Each token should appear as a data: line
        body.Should().Contain("\"type\":\"token\"");
    }

    private static async IAsyncEnumerable<string> AsyncEnumerable(params string[] tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Yield();
            yield return token;
        }
    }
}
