var builder = DistributedApplication.CreateBuilder(args);

// ── External services (Docker containers) ─────────────────────────────────────

// Whisper.cpp ASR server
var whisper = builder.AddContainer("whisper", "ghcr.io/ggml-org/whisper.cpp", "server")
    .WithEndpoint(port: 8081, targetPort: 8081, name: "http")
    .WithArgs(
        "--host", "0.0.0.0",
        "--port", "8081",
        "--model", "/models/ggml-small.bin",
        "--language", "pt",
        "--threads", "4");

// Kokoro TTS server (OpenAI-compatible)
var kokoro = builder.AddContainer("kokoro", "ghcr.io/remsky/kokoro-fastapi-cpu", "latest")
    .WithEndpoint(port: 3000, targetPort: 8880, name: "http")
    .WithEnvironment("KOKORO_VOICES", "af_bella,af_sky");

// Seq structured log server
var seq = builder.AddContainer("seq", "datalust/seq", "latest")
    .WithEndpoint(port: 5341, targetPort: 5341, name: "otlp")
    .WithEndpoint(port: 8088, targetPort: 80, name: "ui")
    .WithEnvironment("ACCEPT_EULA", "Y");

// ── .NET API project ───────────────────────────────────────────────────────────
builder.AddProject<Projects.VoiceAssistLab_Api>("api")
    .WithReference(whisper.GetEndpoint("http"))
    .WithReference(kokoro.GetEndpoint("http"))
    .WithReference(seq.GetEndpoint("otlp"))
    .WithEnvironment("Whisper__BaseUrl", whisper.GetEndpoint("http"))
    .WithEnvironment("Kokoro__BaseUrl", kokoro.GetEndpoint("http"))
    .WithEnvironment("Otel__Endpoint", seq.GetEndpoint("otlp"));

builder.Build().Run();
