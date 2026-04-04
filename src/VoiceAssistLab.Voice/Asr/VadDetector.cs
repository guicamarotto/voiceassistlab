namespace VoiceAssistLab.Voice.Asr;

/// <summary>
/// Voice Activity Detector — tracks silence duration from PCM audio amplitude.
/// Fires IsSpeechEnded when silence exceeds the configured threshold.
/// </summary>
public sealed class VadDetector
{
    private readonly int _silenceThresholdMs;
    private readonly int _sampleRate;
    private readonly double _silenceAmplitudeThreshold;

    private DateTime _lastSpeechAt = DateTime.UtcNow;
    private bool _hasSpeech;

    public VadDetector(
        int silenceThresholdMs = 800,
        int sampleRate = 16000,
        double silenceAmplitudeThreshold = 0.01)
    {
        _silenceThresholdMs = silenceThresholdMs;
        _sampleRate = sampleRate;
        _silenceAmplitudeThreshold = silenceAmplitudeThreshold;
    }

    /// <summary>Returns true if the end of speech has been detected.</summary>
    public bool ProcessChunk(byte[] pcmChunk)
    {
        var rms = ComputeRms(pcmChunk);

        if (rms > _silenceAmplitudeThreshold)
        {
            _lastSpeechAt = DateTime.UtcNow;
            _hasSpeech = true;
            return false;
        }

        var silenceDuration = (DateTime.UtcNow - _lastSpeechAt).TotalMilliseconds;
        return _hasSpeech && silenceDuration >= _silenceThresholdMs;
    }

    public void Reset()
    {
        _lastSpeechAt = DateTime.UtcNow;
        _hasSpeech = false;
    }

    private static double ComputeRms(byte[] pcmChunk)
    {
        if (pcmChunk.Length < 2) return 0;

        // PCM 16-bit LE: each sample is 2 bytes
        var sum = 0.0;
        var sampleCount = pcmChunk.Length / 2;

        for (var i = 0; i < pcmChunk.Length - 1; i += 2)
        {
            var sample = (short)(pcmChunk[i] | (pcmChunk[i + 1] << 8));
            var normalized = sample / 32768.0;
            sum += normalized * normalized;
        }

        return Math.Sqrt(sum / sampleCount);
    }
}
