using System.Text.Json;
using VoiceAssistLab.Core.Tools;

namespace VoiceAssistLab.Infra.MockData;

public sealed class GetReturnPolicyTool(MockDataRepository repo) : IToolExecutor
{
    public string Name => "get_return_policy";
    public string Description => "Retorna a política completa de trocas e devoluções da Loja Demo.";

    public JsonElement Schema => JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new { },
        required = Array.Empty<string>()
    });

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default)
    {
        var policy = await repo.GetReturnPolicyAsync(ct);
        return JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = false });
    }
}
