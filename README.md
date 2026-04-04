# VoiceAssist Lab

A personal learning lab: a **voice-enabled AI customer-support agent** for a fictional e-commerce store, built on **.NET 8**.

```
Browser (mic) ──PCM──▶ WebSocket ──▶ Whisper.cpp (ASR)
                                           │ transcript
                                           ▼
                                  ChatOrchestrator (ILlmClient)
                                   ├─ Groq llama-3.3-70b
                                   └─ Anthropic claude-haiku-4-5
                                           │ tokens (SSE / WS)
                                           ▼
                                    Kokoro TTS ──MP3──▶ Browser
```

---

## Tech Stack

| Layer | Technology | Why |
|---|---|---|
| Runtime | .NET 8, ASP.NET Core Minimal APIs | Modern, fast, first-class async |
| LLM (cloud) | Groq `llama-3.3-70b-versatile` | Free tier, 30 req/min, ultra-fast inference |
| LLM (alt) | Anthropic `claude-haiku-4-5` via `Anthropic.SDK` | Best-in-class reasoning, easy switch |
| LLM abstraction | `ILlmClient` (custom) | Provider-agnostic; avoids MEAI 10.x/.NET 10 conflict |
| ASR | Whisper.cpp HTTP server (Docker) | Free, local, good PT-BR accuracy |
| TTS | Kokoro FastAPI CPU (Docker) | OpenAI-compatible, local, natural voice |
| Voice pipeline | `System.Threading.Channels` | Backpressure, async, no external queue needed |
| Resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8) | DI-native, retry + CB + timeout in 5 lines |
| Observability | OpenTelemetry + Serilog + Seq | Unified traces, metrics, structured logs |
| Orchestration | .NET Aspire AppHost | Single-command startup, service discovery |
| Frontend | Vanilla JS + ES modules + Web Audio API | No build toolchain; Audio API is the learning goal |
| Testing | xUnit + NSubstitute + FluentAssertions + WireMock.Net | NSubstitute handles `IAsyncEnumerable<>` better than Moq |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Whisper, Kokoro, Seq)
- A free [Groq API key](https://console.groq.com/) **or** Anthropic API key
- (Optional) .NET Aspire workload: `dotnet workload install aspire`

Hardware: Whisper `small` model requires ~1 GB RAM. Kokoro CPU inference requires ~2 GB RAM.

---

## Quick Start

```bash
# 1. Clone and configure secrets
git clone <repo-url>
cd VoiceAssistLab
cp .env.example .env
# Edit .env and add your API key

# 2. Start infrastructure (Whisper, Kokoro, Seq)
docker compose -f docker/docker-compose.yml up -d

# 3. Run the API
cd src/VoiceAssistLab.Api
dotnet run

# 4. Open the frontend
# Visit http://localhost:5000 in your browser
```

Alternatively, use the Aspire AppHost for a unified experience:

```bash
dotnet run --project src/VoiceAssistLab.AppHost
# Opens the Aspire dashboard at http://localhost:15000
```

---

## Project Structure

```
VoiceAssistLab/
├── src/
│   ├── VoiceAssistLab.Api/          # Minimal API host: endpoints, DI wiring, middleware
│   ├── VoiceAssistLab.Core/         # Domain: ILlmClient, ChatOrchestrator, Tools, Guardrails
│   ├── VoiceAssistLab.Infra/        # GroqLlmClient, AnthropicLlmClient, CachingLlmClient, MockData
│   ├── VoiceAssistLab.Voice/        # ASR, TTS, VoicePipeline, WebSocketHandler
│   ├── VoiceAssistLab.Resilience/   # Polly pipeline definitions (LLM, ASR, TTS)
│   └── VoiceAssistLab.AppHost/      # Aspire container orchestration
├── tests/
│   └── VoiceAssistLab.Tests/
│       ├── Unit/                    # Isolated tests (NSubstitute mocks)
│       ├── Integration/             # WebApplicationFactory + rate limiter tests
│       └── Golden/                  # 20 fixed-input regression tests (live API)
├── frontend/
│   ├── index.html
│   ├── css/styles.css
│   └── js/                         # main, audioCapture, websocketClient, audioPlayer, chatUi
└── docker/
    └── docker-compose.yml           # Whisper, Kokoro, Seq
```

---

## Configuration

All configuration is via environment variables (or `appsettings.json` for local dev):

| Variable | Default | Description |
|---|---|---|
| `LLM__PROVIDER` | `groq` | LLM provider: `groq` or `anthropic` |
| `LLM__GROQ__APIKEY` | _(required)_ | Groq API key |
| `LLM__GROQ__MODEL` | `llama-3.3-70b-versatile` | Groq model ID |
| `LLM__ANTHROPIC__APIKEY` | _(required if provider=anthropic)_ | Anthropic API key |
| `LLM__ANTHROPIC__MODEL` | `claude-haiku-4-5-20251001` | Anthropic model ID |
| `WHISPER__BASEURL` | `http://localhost:8081` | Whisper.cpp server URL |
| `KOKORO__BASEURL` | `http://localhost:3000` | Kokoro TTS server URL |
| `OTEL__ENDPOINT` | `http://localhost:4317` | OTLP exporter endpoint (e.g. Seq) |

Switch providers with a single variable: `LLM__PROVIDER=anthropic`.

---

## Running Tests

```bash
# Unit + integration tests (no live API required)
dotnet test --filter "Category!=Golden"

# Coverage report
dotnet test --filter "Category!=Golden" --collect:"XPlat Code Coverage"
# Then: reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report

# Golden regression tests (requires live Groq API key)
dotnet test --filter "Category=Golden"
```

---

## Architecture Deep-Dives

### `ILlmClient` Abstraction

`ILlmClient` exposes two methods: `SendAsync` (full response) and `StreamAsync` (token stream). Both providers implement the same interface:

- **Groq**: uses `OpenAI.ChatClient` pointed at Groq's OpenAI-compatible endpoint. Tool format matches OpenAI's function calling spec.
- **Anthropic**: uses `Anthropic.SDK`. Tool parameters use `JsonNode` (not `JsonElement`). The `ToolUseContent.Input` field requires `tu.Input?.ToJsonString()` round-trip to `JsonElement`.

`CachingLlmClient` is a decorator: SHA256 of `(systemPrompt + messages + toolNames)` as cache key, 5-minute TTL, skips caching when tool results appear in history.

> **Why not MEAI?** `Microsoft.Extensions.AI` 10.x pulls in `Microsoft.Extensions.*` 10.x assemblies which conflict with .NET 8's built-in versions. We use the raw SDKs directly instead.

### Voice Pipeline with `Channel<T>`

Three typed channels form an async pipeline:

```
AudioInput (bounded 100)  ──▶  VadDetector  ──▶  WhisperAsr  ──▶  ChatOrchestrator.StreamAsync
                                                                        │ token stream
                                                                   sentence boundary detection
                                                                        │ sentence
                                                                   KokoroTts.SynthesizeStreamAsync
                                                                        │ MP3 chunks
                                                              AudioOutput (bounded 200)  ──▶  WS
```

**Sentence-boundary TTS optimization**: instead of waiting for the full LLM response, we detect the first `.`, `?`, or `!` in the token stream and start TTS synthesis immediately. This saves ~500ms and is the key to achieving the <3s E2E latency target.

**Audio interruption**: sending new voice input cancels the in-flight `CancellationTokenSource` which propagates through all pipeline stages.

### Resilience Patterns

| Pipeline | Retry | Circuit Breaker | Timeout |
|---|---|---|---|
| LLM | 3× exp. backoff (1→4s), on 5xx/429 | 5 failures / 30s break | 10s |
| ASR | 2× fixed 500ms | — | 5s |
| TTS | — | — | 5s first byte; empty audio fallback |

Polly pipelines are registered via `IHttpClientBuilder.AddResilienceHandler` for ASR and TTS (which use typed `HttpClient`). The LLM clients are wrapped manually since they use provider SDKs.

### Observability

- **Traces**: `ActivitySource("VoiceAssistLab.Chat")` creates spans for `llm.send`, `llm.tool_call`, `llm.stream`. Exported via OTLP to Seq or the Aspire dashboard.
- **Metrics**: `Meter("VoiceAssistLab")` exposes:
  - `llm.tokens` (counter) — total tokens consumed
  - `llm.ttft_ms` (histogram) — time to first token
  - `llm.tool_calls` (counter) — tool dispatches
- **Logs**: Serilog with `ReadFrom.Configuration()`, enriched with `TraceId`, `SpanId`, `MachineName`. Console output in dev; OTLP sink in prod.

---

## Lessons Learned

### Phase 1 — LLM Integration
- **MEAI 10.x vs .NET 8**: `Microsoft.Extensions.AI` 10.x requires `Microsoft.Extensions.*` 10.x assemblies which conflict with .NET 8's built-in versions at runtime. Solution: abandon MEAI, use provider SDKs directly. The raw `OpenAI.ChatClient` and `Anthropic.SDK` are simpler anyway.
- **Anthropic SDK quirks**: `ToolUseContent.Input` is `JsonNode`, not `JsonElement`. Tool function constructor requires `JsonNode.Parse()` not direct `JsonElement`. These are not documented prominently.
- **Swashbuckle vs Scalar**: `AddOpenApi()`/`MapOpenApi()` are .NET 9+ only. Swashbuckle 6.x works on .NET 8.

### Phase 2 — Voice
- **Whisper audio format**: expects WAV 16kHz mono 16-bit PCM. The browser's `getUserMedia` produces WebM/Opus. Solution: `AudioWorklet` for real-time PCM conversion in the browser + `WavEncoder` server-side wrapper. Do this first.
- **Kokoro first-byte latency**: 800ms–1.5s on CPU. Without sentence-boundary splitting, E2E latency exceeded 3s. The sentence-boundary optimization brings it back under target.
- **AudioWorklet from Blob URL**: registering the worklet inline via `URL.createObjectURL(new Blob([src]))` avoids needing a separate file on the server.

### Phase 3 — Reliability
- **Rate limiter partition key**: using a session cookie as the partition key is better than IP for users behind NAT. Fall back to IP if cookie is absent.
- **Golden test rate limits**: Groq's 30 req/min limit means golden tests must run sequentially with 2s delay. Batching or parallel execution triggers 429s.

---

## Known Limitations & Future Ideas

- **No persistent storage**: conversation history is session-only (in-memory). No database.
- **Groq rate limits**: free tier is 30 req/min. Golden tests require throttling.
- **Kokoro CPU latency**: ~1s first byte. GPU deployment or a different TTS service would improve latency significantly.

**Ideas for future phases:**
- **Ollama** as a third provider for fully local inference
- **SQLite** with EF Core for real order data instead of mock JSON
- **Azure Container Apps** deployment with managed identity
- **WebRTC** instead of raw WebSocket for better audio quality
- **Streaming tool calls** (Groq supports this) to reduce the peek-then-stream double-call

---

## License

MIT
