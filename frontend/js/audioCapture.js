// audioCapture.js — getUserMedia + AudioWorklet for 16kHz mono PCM capture

const SAMPLE_RATE = 16_000;
const CHUNK_INTERVAL_MS = 100;

/**
 * Inline AudioWorklet processor source.
 * Receives raw Float32 samples from the audio graph and accumulates them
 * into fixed-size chunks (every ~100ms at 16kHz = 1600 frames).
 */
const WORKLET_SRC = `
class PcmProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    this._buffer = [];
    this._chunkSize = ${Math.round(SAMPLE_RATE * CHUNK_INTERVAL_MS / 1000)};
  }

  process(inputs) {
    const channel = inputs[0]?.[0];
    if (!channel) return true;

    for (let i = 0; i < channel.length; i++) {
      this._buffer.push(channel[i]);
    }

    while (this._buffer.length >= this._chunkSize) {
      const chunk = this._buffer.splice(0, this._chunkSize);
      // Convert Float32 to Int16 PCM
      const pcm16 = new Int16Array(chunk.length);
      for (let j = 0; j < chunk.length; j++) {
        const s = Math.max(-1, Math.min(1, chunk[j]));
        pcm16[j] = s < 0 ? s * 0x8000 : s * 0x7fff;
      }
      this.port.postMessage(pcm16.buffer, [pcm16.buffer]);
    }
    return true;
  }
}
registerProcessor('pcm-processor', PcmProcessor);
`;

export class AudioCapture {
  constructor(onChunk) {
    this._onChunk = onChunk; // (ArrayBuffer) => void
    this._context = null;
    this._stream = null;
    this._workletNode = null;
    this._sourceNode = null;
  }

  async start() {
    if (this._context) return;

    this._stream = await navigator.mediaDevices.getUserMedia({
      audio: {
        sampleRate: SAMPLE_RATE,
        channelCount: 1,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });

    this._context = new AudioContext({ sampleRate: SAMPLE_RATE });

    // Register the worklet from a Blob URL so we don't need a separate file
    const blob = new Blob([WORKLET_SRC], { type: 'application/javascript' });
    const url = URL.createObjectURL(blob);
    await this._context.audioWorklet.addModule(url);
    URL.revokeObjectURL(url);

    this._workletNode = new AudioWorkletNode(this._context, 'pcm-processor');
    this._workletNode.port.onmessage = (e) => {
      if (e.data instanceof ArrayBuffer) {
        this._onChunk(e.data);
      }
    };

    this._sourceNode = this._context.createMediaStreamSource(this._stream);
    this._sourceNode.connect(this._workletNode);
    // Not connecting to destination — we only want to capture, not play back
  }

  stop() {
    this._sourceNode?.disconnect();
    this._workletNode?.disconnect();
    this._stream?.getTracks().forEach((t) => t.stop());
    this._context?.close();
    this._context = null;
    this._stream = null;
    this._workletNode = null;
    this._sourceNode = null;
  }
}
