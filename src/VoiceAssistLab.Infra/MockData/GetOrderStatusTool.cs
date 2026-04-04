using System.Text.Json;
using VoiceAssistLab.Core.Tools;

namespace VoiceAssistLab.Infra.MockData;

public sealed class GetOrderStatusTool(MockDataRepository repo) : IToolExecutor
{
    public string Name => "get_order_status";
    public string Description => "Consulta o status de um pedido pelo ID do pedido.";

    public JsonElement Schema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            order_id = new { type = "string", description = "O ID do pedido, ex: ORD-001" }
        },
        required = new[] { "order_id" }
    });

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("order_id", out var orderIdProp))
            return "Erro: parâmetro 'order_id' é obrigatório.";

        var orderId = orderIdProp.GetString();
        if (string.IsNullOrWhiteSpace(orderId))
            return "Erro: 'order_id' não pode ser vazio.";

        var order = await repo.GetOrderAsync(orderId, ct);
        if (order is null)
            return $"Pedido '{orderId}' não encontrado.";

        return JsonSerializer.Serialize(order, new JsonSerializerOptions { WriteIndented = false });
    }
}
