namespace VoiceAssistLab.Voice.Tts;

public sealed class KokoroOptions
{
    public string BaseUrl { get; set; } = "http://localhost:3000";
    public string Voice { get; set; } = "af_heart";
    public string Format { get; set; } = "mp3";
    public int TimeoutSeconds { get; set; } = 5;
}
