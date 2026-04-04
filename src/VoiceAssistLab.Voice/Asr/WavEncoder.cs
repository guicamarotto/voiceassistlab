namespace VoiceAssistLab.Voice.Asr;

/// <summary>Encodes raw PCM audio data into a WAV file with the required 44-byte header.</summary>
public static class WavEncoder
{
    public static byte[] Encode(byte[] pcmData, int sampleRate = 16000, int channels = 1, int bitsPerSample = 16)
    {
        var dataLength = pcmData.Length;
        var fileSize = 44 + dataLength;
        var buffer = new byte[fileSize];

        // RIFF chunk
        buffer[0] = (byte)'R'; buffer[1] = (byte)'I'; buffer[2] = (byte)'F'; buffer[3] = (byte)'F';
        WriteInt32LE(buffer, 4, fileSize - 8);
        buffer[8] = (byte)'W'; buffer[9] = (byte)'A'; buffer[10] = (byte)'V'; buffer[11] = (byte)'E';

        // fmt chunk
        buffer[12] = (byte)'f'; buffer[13] = (byte)'m'; buffer[14] = (byte)'t'; buffer[15] = (byte)' ';
        WriteInt32LE(buffer, 16, 16);             // fmt chunk size
        WriteInt16LE(buffer, 20, 1);              // PCM format
        WriteInt16LE(buffer, 22, (short)channels);
        WriteInt32LE(buffer, 24, sampleRate);
        WriteInt32LE(buffer, 28, sampleRate * channels * bitsPerSample / 8); // byte rate
        WriteInt16LE(buffer, 32, (short)(channels * bitsPerSample / 8));     // block align
        WriteInt16LE(buffer, 34, (short)bitsPerSample);

        // data chunk
        buffer[36] = (byte)'d'; buffer[37] = (byte)'a'; buffer[38] = (byte)'t'; buffer[39] = (byte)'a';
        WriteInt32LE(buffer, 40, dataLength);
        Array.Copy(pcmData, 0, buffer, 44, dataLength);

        return buffer;
    }

    private static void WriteInt32LE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static void WriteInt16LE(byte[] buffer, int offset, short value)
    {
        buffer[offset] = (byte)(value & 0xFF);
        buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
    }
}
