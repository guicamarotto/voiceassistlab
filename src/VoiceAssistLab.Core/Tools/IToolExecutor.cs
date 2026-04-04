using System.Text.Json;

namespace VoiceAssistLab.Core.Tools;

public interface IToolExecutor
{
    string Name { get; }
    string Description { get; }
    JsonElement Schema { get; }
    Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct = default);
}
