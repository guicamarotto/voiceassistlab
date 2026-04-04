using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using VoiceAssistLab.Core.Chat;
using VoiceAssistLab.Voice.Asr;
using VoiceAssistLab.Voice.Tts;

namespace VoiceAssistLab.Voice.Pipeline;

/// <summary>
/// Orchestrates the full voice pipeline:
/// PCM audio → VAD → ASR (Whisper) → LLM (ChatOrchestrator) → TTS (Kokoro) → audio chunks
/// Uses Channel&lt;T&gt; for non-blocking async stages.
/// </summary>
public sealed class VoicePipeline(
    IAsrClient asr,
    ITtsClient tts,
    IChatOrchestrator orchestrator,
    ILogger<VoicePipeline> logger)
{
    // Channels connecting pipeline stages
    private readonly Channel<byte[]> _audioInputChannel = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest });

    private readonly Channel<byte[]> _audioOutputChannel = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.Wait });

    private readonly Channel<string> _transcriptChannel = Channel.CreateUnbounded<string>();

    public ChannelWriter<byte[]> AudioInput => _audioInputChannel.Writer;
    public ChannelReader<byte[]> AudioOutput => _audioOutputChannel.Reader;
    public ChannelReader<string> TranscriptOutput => _transcriptChannel.Reader;

    /// <summary>Runs all pipeline stages until cancellation is requested.</summary>
    public async Task RunAsync(string? sessionId, CancellationToken ct)
    {
        var vad = new VadDetector();
        var pcmBuffer = new List<byte>();

        await foreach (var chunk in _audioInputChannel.Reader.ReadAllAsync(ct))
        {
            pcmBuffer.AddRange(chunk);
            var speechEnded = vad.ProcessChunk(chunk);

            if (!speechEnded) continue;

            // Snapshot buffer, reset, start processing
            var pcmData = pcmBuffer.ToArray();
            pcmBuffer.Clear();
            vad.Reset();

            await ProcessUtteranceAsync(pcmData, sessionId, ct);
        }
    }

    private async Task ProcessUtteranceAsync(byte[] pcmData, string? sessionId, CancellationToken ct)
    {
        // Stage 1: ASR
        logger.LogInformation("Starting ASR transcription, audio length: {Bytes} bytes", pcmData.Length);
        var transcript = await asr.TranscribeAsync(pcmData, ct);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            logger.LogDebug("Empty transcript — skipping");
            return;
        }

        logger.LogInformation("Transcript: {Transcript}", transcript);
        await _transcriptChannel.Writer.WriteAsync(transcript, ct);

        // Stage 2: LLM (stream tokens, detect sentence boundaries for early TTS)
        var request = new ChatRequest(transcript, SessionId: sessionId);
        var textBuffer = new System.Text.StringBuilder();

        await foreach (var token in orchestrator.StreamAsync(request, ct))
        {
            textBuffer.Append(token);

            // Sentence boundary detection — start TTS on first complete sentence
            var text = textBuffer.ToString();
            var boundaryIdx = FindSentenceBoundary(text);

            if (boundaryIdx <= 0) continue;

            var sentence = text[..boundaryIdx].Trim();
            textBuffer.Remove(0, boundaryIdx);

            if (!string.IsNullOrEmpty(sentence))
                await SynthesizeAndQueueAsync(sentence, ct);
        }

        // Flush any remaining text
        var remaining = textBuffer.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
            await SynthesizeAndQueueAsync(remaining, ct);
    }

    private async Task SynthesizeAndQueueAsync(string sentence, CancellationToken ct)
    {
        logger.LogDebug("Synthesizing sentence: {Sentence}", sentence[..Math.Min(50, sentence.Length)]);

        await foreach (var audioChunk in tts.SynthesizeStreamAsync(sentence, ct))
        {
            await _audioOutputChannel.Writer.WriteAsync(audioChunk, ct);
        }
    }

    /// <summary>
    /// Finds the index after the first sentence-ending punctuation (. ? !) followed by a space or end.
    /// Returns -1 if no sentence boundary is found.
    /// </summary>
    private static int FindSentenceBoundary(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is '.' or '?' or '!')
            {
                // End of string or followed by space
                if (i == text.Length - 1 || text[i + 1] == ' ')
                    return i + 1;
            }
        }
        return -1;
    }
}
