namespace VoiceAssistLab.Voice.Asr;

public sealed class WhisperOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8081";
    public string Language { get; set; } = "pt";
    public int TimeoutSeconds { get; set; } = 5;
}
