using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using VoiceAssistLab.Core.Llm;

namespace VoiceAssistLab.Tests.Integration;

/// <summary>
/// Verifies that the rate limiter rejects the 11th request within a 60-second window.
/// Uses a fixed session-id cookie so all requests share the same partition key.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RateLimiterTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public RateLimiterTests(WebApplicationFactory<Program> factory)
    {
        var mockLlm = Substitute.For<ILlmClient>();
        mockLlm.SendAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse("ok", null, new LlmUsage(1, 1)));
        mockLlm.StreamAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable("ok"));

        _client = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var d = services.SingleOrDefault(s => s.ServiceType == typeof(ILlmClient));
                if (d is not null) services.Remove(d);
                services.AddSingleton(mockLlm);
            }))
            .CreateClient();

        // Use a fixed session-id so all requests share the same rate-limit bucket
        _client.DefaultRequestHeaders.Add("Cookie", "session-id=test-rate-limit-session");
    }

    [Fact]
    public async Task EleventhRequest_WithinOneMinute_Returns429()
    {
        // Send 10 requests that should all succeed
        for (var i = 0; i < 10; i++)
        {
            var r = await _client.PostAsJsonAsync("/api/chat", new { message = $"ping {i}" });
            r.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests,
                because: $"request {i + 1} should be within the rate limit");
        }

        // 11th request should be rejected
        var rejected = await _client.PostAsJsonAsync("/api/chat", new { message = "too many" });
        rejected.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
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
