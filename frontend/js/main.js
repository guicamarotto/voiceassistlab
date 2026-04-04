// main.js — entry point, wires all modules together

import { AudioCapture } from './audioCapture.js';
import { AudioPlayer } from './audioPlayer.js';
import { VoiceWebSocketClient } from './websocketClient.js';
import {
  appendMessage,
  appendLoadingBubble,
  appendToken,
  finalizeLoadingBubble,
  setTranscript,
  setMicState,
} from './chatUi.js';

// ─── Configuration ────────────────────────────────────────────────────────────

const API_BASE = `${location.protocol}//${location.host}`;
const WS_BASE = `${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}`;
const CHAT_URL = `${API_BASE}/api/chat`;
const WS_URL = `${WS_BASE}/ws/voice`;

// ─── State ────────────────────────────────────────────────────────────────────

let micActive = false;
let currentAssistantBubble = null;

// ─── Text chat ────────────────────────────────────────────────────────────────

const textForm = document.getElementById('text-form');
const textInput = document.getElementById('text-input');
const sendBtn = document.getElementById('send-btn');

textForm.addEventListener('submit', async (e) => {
  e.preventDefault();
  const message = textInput.value.trim();
  if (!message) return;

  textInput.value = '';
  sendBtn.disabled = true;

  appendMessage('user', message);
  const bubble = appendLoadingBubble();
  currentAssistantBubble = bubble;
  let accumulated = '';

  try {
    const res = await fetch(CHAT_URL, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message }),
    });

    if (!res.ok || !res.body) {
      finalizeLoadingBubble(bubble, 'Erro ao conectar com o assistente.');
      return;
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let partial = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      partial += decoder.decode(value, { stream: true });
      const lines = partial.split('\n');
      partial = lines.pop(); // keep incomplete last line

      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        const payload = line.slice(6).trim();
        if (payload === '[DONE]') break;

        try {
          const evt = JSON.parse(payload);
          if (evt.token) {
            accumulated += evt.token;
            appendToken(bubble, evt.token);
          }
        } catch {
          // ignore malformed SSE lines
        }
      }
    }

    if (!accumulated) {
      finalizeLoadingBubble(bubble, 'Desculpe, não consegui processar sua solicitação.');
    }
  } catch (err) {
    finalizeLoadingBubble(bubble, 'Erro de rede. Verifique sua conexão.');
    console.error('[chat]', err);
  } finally {
    sendBtn.disabled = false;
    textInput.focus();
    currentAssistantBubble = null;
  }
});

// ─── Voice chat ───────────────────────────────────────────────────────────────

const player = new AudioPlayer();

player.onPlaybackStart = () => setMicState('playing');
player.onPlaybackEnd = () => {
  if (micActive) setMicState('recording');
  else setMicState('idle');
};

const wsClient = new VoiceWebSocketClient({
  url: WS_URL,
  onAudioChunk: (buf) => player.enqueue(buf),
  onEvent: (evt) => {
    if (evt.type === 'transcript') {
      setTranscript(evt.text ?? '');
      if (evt.text) appendMessage('user', evt.text);
    } else if (evt.type === 'response_start') {
      currentAssistantBubble = appendLoadingBubble();
    } else if (evt.type === 'token' && currentAssistantBubble) {
      appendToken(currentAssistantBubble, evt.text ?? '');
    } else if (evt.type === 'response_end') {
      currentAssistantBubble = null;
    } else if (evt.type === 'error') {
      if (currentAssistantBubble) {
        finalizeLoadingBubble(currentAssistantBubble, evt.message ?? 'Erro no processamento de voz.');
        currentAssistantBubble = null;
      }
    }
  },
  onOpen: () => console.info('[ws] connected'),
  onClose: (reason) => {
    console.info('[ws] closed:', reason);
    if (micActive) stopMic();
  },
  onError: (e) => console.error('[ws] error', e),
});

const capture = new AudioCapture((pcmBuffer) => {
  wsClient.sendAudio(pcmBuffer);
});

const micBtn = document.getElementById('mic-btn');
micBtn.addEventListener('click', async () => {
  if (!micActive) {
    await startMic();
  } else {
    stopMic();
  }
});

async function startMic() {
  try {
    await capture.start();
    wsClient.connect();
    micActive = true;
    setMicState('recording');
  } catch (err) {
    console.error('[mic] failed to start:', err);
    alert('Não foi possível acessar o microfone. Verifique as permissões do navegador.');
  }
}

function stopMic() {
  capture.stop();
  wsClient.disconnect();
  player.stop();
  micActive = false;
  setMicState('idle');
  setTranscript('');
}
