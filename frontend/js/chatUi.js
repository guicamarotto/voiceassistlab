// chatUi.js — DOM manipulation for chat history, transcript, and mic button states

const chatHistory = document.getElementById('chat-history');
const transcriptEl = document.getElementById('transcript');
const voiceStatus = document.getElementById('voice-status');
const micBtn = document.getElementById('mic-btn');

/**
 * Append a message bubble to the chat history.
 * @param {'user'|'assistant'} role
 * @param {string} text
 * @returns {HTMLElement} the bubble element (useful for streaming updates)
 */
export function appendMessage(role, text = '') {
  const wrapper = document.createElement('div');
  wrapper.className = `chat-message ${role}`;

  const role_label = document.createElement('span');
  role_label.className = 'chat-role';
  role_label.textContent = role === 'user' ? 'Você' : 'Assistente';

  const bubble = document.createElement('div');
  bubble.className = 'chat-bubble';
  bubble.textContent = text;

  wrapper.appendChild(role_label);
  wrapper.appendChild(bubble);
  chatHistory.appendChild(wrapper);
  scrollToBottom();

  return bubble;
}

/**
 * Add a loading bubble that will be replaced when content arrives.
 * @returns {HTMLElement} bubble element
 */
export function appendLoadingBubble() {
  const wrapper = document.createElement('div');
  wrapper.className = 'chat-message assistant';

  const role_label = document.createElement('span');
  role_label.className = 'chat-role';
  role_label.textContent = 'Assistente';

  const bubble = document.createElement('div');
  bubble.className = 'chat-bubble loading';

  wrapper.appendChild(role_label);
  wrapper.appendChild(bubble);
  chatHistory.appendChild(wrapper);
  scrollToBottom();

  return bubble;
}

/** Remove the loading class and set the final text on a bubble. */
export function finalizeLoadingBubble(bubble, text) {
  bubble.classList.remove('loading');
  bubble.textContent = text;
  scrollToBottom();
}

/** Append text tokens to an existing bubble (streaming). */
export function appendToken(bubble, token) {
  bubble.classList.remove('loading');
  bubble.textContent += token;
  scrollToBottom();
}

/** Update the live transcript display. */
export function setTranscript(text) {
  transcriptEl.textContent = text;
}

/** Update the voice status label. */
export function setVoiceStatus(text) {
  voiceStatus.textContent = text;
}

/**
 * Set the mic button visual state.
 * @param {'idle'|'recording'|'processing'|'playing'} state
 */
export function setMicState(state) {
  micBtn.classList.remove('idle', 'recording', 'processing', 'playing');
  micBtn.classList.add(state);

  const labels = {
    idle: 'Iniciar gravação de voz',
    recording: 'Parar gravação',
    processing: 'Processando…',
    playing: 'Reproduzindo resposta',
  };
  micBtn.setAttribute('aria-label', labels[state] ?? state);

  const statuses = {
    idle: 'Pronto para ouvir',
    recording: 'Gravando… fale agora',
    processing: 'Processando sua voz…',
    playing: 'Reproduzindo resposta…',
  };
  setVoiceStatus(statuses[state] ?? state);
}

function scrollToBottom() {
  chatHistory.scrollTop = chatHistory.scrollHeight;
}
