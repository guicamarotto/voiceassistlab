// audioPlayer.js — decode incoming MP3/audio chunks and play them sequentially

export class AudioPlayer {
  constructor() {
    this._context = null;
    this._queue = []; // ArrayBuffer[]
    this._playing = false;
    this._onStart = null;  // () => void — fired when playback starts
    this._onEnd = null;    // () => void — fired when the queue drains
  }

  /** @param {() => void} cb */
  set onPlaybackStart(cb) { this._onStart = cb; }

  /** @param {() => void} cb */
  set onPlaybackEnd(cb) { this._onEnd = cb; }

  /** Enqueue a raw audio chunk (MP3 or WAV ArrayBuffer) for playback. */
  enqueue(chunk) {
    this._queue.push(chunk);
    if (!this._playing) {
      this._playNext();
    }
  }

  /** Stop all queued audio immediately. */
  stop() {
    this._queue = [];
    this._playing = false;
    if (this._context) {
      this._context.close();
      this._context = null;
    }
    this._onEnd?.();
  }

  async _playNext() {
    if (this._queue.length === 0) {
      this._playing = false;
      this._onEnd?.();
      return;
    }

    this._playing = true;

    if (!this._context || this._context.state === 'closed') {
      this._context = new AudioContext();
    }

    if (this._context.state === 'suspended') {
      await this._context.resume();
    }

    const chunk = this._queue.shift();

    let buffer;
    try {
      buffer = await this._context.decodeAudioData(chunk.slice(0)); // slice = defensive copy
    } catch {
      // Skip undecodable chunk (e.g. empty or malformed)
      this._playNext();
      return;
    }

    const source = this._context.createBufferSource();
    source.buffer = buffer;
    source.connect(this._context.destination);

    if (!this._playing) {
      // Was stopped while decoding
      return;
    }

    this._onStart?.();
    source.start();
    source.onended = () => {
      this._playNext();
    };
  }
}
