using System.Text.Json;
using System.Text.Json.Nodes;

namespace VoiceAssistLab.Infra.MockData;

public sealed class MockDataRepository
{
    private readonly JsonArray _orders;
    private readonly JsonArray _products;
    private readonly JsonObject _returnPolicy;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public MockDataRepository()
    {
        _orders = LoadJsonArray("VoiceAssistLab.Infra.MockData.orders.json");
        _products = LoadJsonArray("VoiceAssistLab.Infra.MockData.products.json");
        _returnPolicy = LoadJsonObject("VoiceAssistLab.Infra.MockData.return-policy.json");
    }

    public Task<JsonObject?> GetOrderAsync(string orderId, CancellationToken ct = default)
    {
        var order = _orders
            .OfType<JsonObject>()
            .FirstOrDefault(o => o["id"]?.GetValue<string>()
                .Equals(orderId, StringComparison.OrdinalIgnoreCase) == true);
        return Task.FromResult(order);
    }

    public Task<JsonObject?> GetProductAsync(string productId, CancellationToken ct = default)
    {
        var product = _products
            .OfType<JsonObject>()
            .FirstOrDefault(p => p["id"]?.GetValue<string>()
                .Equals(productId, StringComparison.OrdinalIgnoreCase) == true);
        return Task.FromResult(product);
    }

    public Task<JsonObject> GetReturnPolicyAsync(CancellationToken ct = default)
        => Task.FromResult(_returnPolicy);

    private static JsonArray LoadJsonArray(string resourceName)
    {
        using var stream = typeof(MockDataRepository).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        return JsonNode.Parse(stream)?.AsArray()
            ?? throw new InvalidOperationException($"Resource '{resourceName}' is not a JSON array.");
    }

    private static JsonObject LoadJsonObject(string resourceName)
    {
        using var stream = typeof(MockDataRepository).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        return JsonNode.Parse(stream)?.AsObject()
            ?? throw new InvalidOperationException($"Resource '{resourceName}' is not a JSON object.");
    }
}
