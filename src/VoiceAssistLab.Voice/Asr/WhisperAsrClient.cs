using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VoiceAssistLab.Voice.Asr;

public sealed class WhisperAsrClient(
    HttpClient http,
    IOptions<WhisperOptions> options,
    ILogger<WhisperAsrClient> logger) : IAsrClient
{
    private readonly WhisperOptions _options = options.Value;

    public async Task<string> TranscribeAsync(byte[] pcmAudio, CancellationToken ct = default)
    {
        var wavData = WavEncoder.Encode(pcmAudio, sampleRate: 16000);

        using var form = new MultipartFormDataContent();
        using var audioContent = new ByteArrayContent(wavData);
        audioContent.Headers.ContentType = new("audio/wav");
        form.Add(audioContent, "audio_file", "audio.wav");

        // onerahmet/openai-whisper-asr-webservice expects language/task as query params
        var url = $"/asr?encode=true&task=transcribe&language={Uri.EscapeDataString(_options.Language)}&output=json";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        try
        {
            var response = await http.PostAsync(url, form, cts.Token);
            response.EnsureSuccessStatusCode();

            // Whisper.cpp server returns JSON: { "text": "..." }
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            return json.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "ASR transcription failed");
            return string.Empty;
        }
    }
}
