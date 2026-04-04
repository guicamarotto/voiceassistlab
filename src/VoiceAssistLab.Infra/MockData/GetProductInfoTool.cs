using System.Text.Json;
using VoiceAssistLab.Core.Tools;

namespace VoiceAssistLab.Infra.MockData;

public sealed class GetProductInfoTool(MockDataRepository repo) : IToolExecutor
{
    public string Name => "get_product_info";
    public string Description => "Obtém informações detalhadas de um produto pelo ID do produto.";

    public JsonElement Schema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            product_id = new { type = "string", description = "O ID do produto, ex: PROD-001" }
        },
        required = new[] { "product_id" }
    });

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("product_id", out var productIdProp))
            return "Erro: parâmetro 'product_id' é obrigatório.";

        var productId = productIdProp.GetString();
        if (string.IsNullOrWhiteSpace(productId))
            return "Erro: 'product_id' não pode ser vazio.";

        var product = await repo.GetProductAsync(productId, ct);
        if (product is null)
            return $"Produto '{productId}' não encontrado.";

        return JsonSerializer.Serialize(product, new JsonSerializerOptions { WriteIndented = false });
    }
}
