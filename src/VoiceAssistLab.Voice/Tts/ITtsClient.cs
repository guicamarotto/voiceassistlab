namespace VoiceAssistLab.Voice.Tts;

public interface ITtsClient
{
    /// <summary>Synthesizes text to speech and streams audio chunks.</summary>
    IAsyncEnumerable<byte[]> SynthesizeStreamAsync(string text, CancellationToken ct = default);
}
