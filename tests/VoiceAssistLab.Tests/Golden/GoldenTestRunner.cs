using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VoiceAssistLab.Tests.Golden;

/// <summary>
/// Golden regression tests using the live LLM API (Groq by default).
/// Run with: dotnet test --filter "Category=Golden"
/// Requires LLM__GROQ__APIKEY environment variable to be set.
/// Tests run sequentially with a 2-second delay to respect the 30 req/min Groq rate limit.
/// </summary>
[Trait("Category", "Golden")]
public sealed class GoldenTestRunner : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public GoldenTestRunner(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    public static IEnumerable<object[]> GoldenCases()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Golden", "golden-cases.json"));
        var cases = JsonSerializer.Deserialize<List<GoldenCase>>(json, JsonOpts)!;
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(GoldenCases))]
    public async Task GoldenCase_ShouldProduceExpectedResponse(GoldenCase testCase)
    {
        // Respect Groq rate limit: 30 req/min → 2s between requests
        await Task.Delay(TimeSpan.FromSeconds(2));

        var response = await _client.PostAsJsonAsync("/api/chat",
            new { message = testCase.Input });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Golden case {testCase.Id} should succeed");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("[DONE]", because: "SSE stream must terminate");

        // Extract all token content from SSE lines
        var fullText = ExtractSseText(body);

        foreach (var expected in testCase.ResponseMustContain)
        {
            fullText.Should().ContainEquivalentOf(expected,
                because: $"Golden case {testCase.Id} response must contain '{expected}'");
        }
    }

    private static string ExtractSseText(string sseBody)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var line in sseBody.Split('\n'))
        {
            if (!line.StartsWith("data: ")) continue;
            var payload = line[6..].Trim();
            if (payload == "[DONE]") break;

            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("content", out var content))
                    sb.Append(content.GetString());
            }
            catch { /* skip malformed lines */ }
        }
        return sb.ToString();
    }

    public sealed class GoldenCase
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = "";

        [JsonPropertyName("input")]
        public string Input { get; init; } = "";

        [JsonPropertyName("expected_intent")]
        public string ExpectedIntent { get; init; } = "";

        [JsonPropertyName("tools_expected")]
        public List<string> ToolsExpected { get; init; } = [];

        [JsonPropertyName("response_must_contain")]
        public List<string> ResponseMustContain { get; init; } = [];
    }
}
