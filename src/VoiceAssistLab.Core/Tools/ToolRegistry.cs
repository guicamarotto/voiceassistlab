namespace VoiceAssistLab.Core.Tools;

public sealed class ToolRegistry
{
    private readonly Dictionary<string, IToolExecutor> _tools;

    public ToolRegistry(IEnumerable<IToolExecutor> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IToolExecutor> All => _tools.Values;

    public IToolExecutor? Get(string name)
        => _tools.TryGetValue(name, out var tool) ? tool : null;

    public async Task<string> ExecuteAsync(string toolName, System.Text.Json.JsonElement arguments, CancellationToken ct = default)
    {
        var tool = Get(toolName)
            ?? throw new InvalidOperationException($"Unknown tool: '{toolName}'");
        return await tool.ExecuteAsync(arguments, ct);
    }
}
