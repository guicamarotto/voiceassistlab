namespace VoiceAssistLab.Voice.Asr;

public interface IAsrClient
{
    /// <summary>Transcribes PCM audio data and returns the text.</summary>
    Task<string> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default);
}
