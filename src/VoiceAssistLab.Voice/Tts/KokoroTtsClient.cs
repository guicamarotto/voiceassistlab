using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VoiceAssistLab.Voice.Tts;

public sealed class KokoroTtsClient(
    HttpClient http,
    IOptions<KokoroOptions> options,
    ILogger<KokoroTtsClient> logger) : ITtsClient
{
    private readonly KokoroOptions _options = options.Value;

    public async IAsyncEnumerable<byte[]> SynthesizeStreamAsync(
        string text,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var requestBody = JsonSerializer.Serialize(new
        {
            model = "kokoro",
            input = text,
            voice = _options.Voice,
            response_format = _options.Format,
            speed = 1.0
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/audio/speech")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
        };

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "TTS synthesis request failed");
            yield break; // Graceful degradation — return no audio
        }

        using (response)
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[4096];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                var chunk = new byte[bytesRead];
                Array.Copy(buffer, chunk, bytesRead);
                yield return chunk;
            }
        }
    }
}
