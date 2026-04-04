namespace VoiceAssistLab.Infra.Llm;

public sealed class AnthropicOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
}
