// websocketClient.js — WebSocket lifecycle, binary PCM frames out, audio/text frames in

export class VoiceWebSocketClient {
  /**
   * @param {object} opts
   * @param {string} opts.url — ws:// or wss:// endpoint
   * @param {(chunk: ArrayBuffer) => void} opts.onAudioChunk — incoming audio bytes
   * @param {(event: {type: string, text?: string}) => void} opts.onEvent — incoming JSON events
   * @param {() => void} [opts.onOpen]
   * @param {(reason: string) => void} [opts.onClose]
   * @param {(err: Event) => void} [opts.onError]
   */
  constructor({ url, onAudioChunk, onEvent, onOpen, onClose, onError }) {
    this._url = url;
    this._onAudioChunk = onAudioChunk;
    this._onEvent = onEvent;
    this._onOpen = onOpen;
    this._onClose = onClose;
    this._onError = onError;
    this._ws = null;
  }

  connect() {
    if (this._ws) return;

    this._ws = new WebSocket(this._url);
    this._ws.binaryType = 'arraybuffer';

    this._ws.onopen = () => {
      this._onOpen?.();
    };

    this._ws.onclose = (e) => {
      this._ws = null;
      this._onClose?.(e.reason || 'connection closed');
    };

    this._ws.onerror = (e) => {
      this._onError?.(e);
    };

    this._ws.onmessage = (e) => {
      if (e.data instanceof ArrayBuffer) {
        // Binary frame = audio chunk
        this._onAudioChunk(e.data);
      } else if (typeof e.data === 'string') {
        try {
          const parsed = JSON.parse(e.data);
          this._onEvent(parsed);
        } catch {
          // Ignore unparseable text frames
        }
      }
    };
  }

  /** Send a raw PCM Int16 chunk to the server. */
  sendAudio(arrayBuffer) {
    if (this._ws?.readyState === WebSocket.OPEN) {
      this._ws.send(arrayBuffer);
    }
  }

  disconnect() {
    if (this._ws) {
      this._ws.close();
      this._ws = null;
    }
  }

  get isConnected() {
    return this._ws?.readyState === WebSocket.OPEN;
  }
}
