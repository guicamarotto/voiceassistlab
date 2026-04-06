using System.Threading.RateLimiting;
using Serilog;
using VoiceAssistLab.Api.DependencyInjection;
using VoiceAssistLab.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .Enrich.WithMachineName()
       .WriteTo.Console(outputTemplate:
           "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

// OpenAPI (Swashbuckle for .NET 8)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
    opts.SwaggerDoc("v1", new() { Title = "VoiceAssist Lab API", Version = "v1" }));

// Domain services
builder.Services.AddCoreServices();

// LLM (provider selected via Llm:Provider config)
builder.Services.AddLlmClient(builder.Configuration);

// Voice (ASR, TTS, pipeline)
builder.Services.AddVoiceServices(builder.Configuration);

// OpenTelemetry
builder.Services.AddObservability(builder.Configuration);

// Rate limiting — fixed window: 10 requests/min per session cookie (or IP fallback)
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opts.AddPolicy("chat", httpContext =>
    {
        // Use session-id cookie if present, otherwise fall back to IP
        var sessionId = httpContext.Request.Cookies["session-id"]
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(sessionId, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
    });
});

var app = builder.Build();

// WebSockets must be before routing
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });

app.UseSerilogRequestLogging();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(opts => opts.SwaggerEndpoint("/swagger/v1/swagger.json", "VoiceAssist Lab v1"));
}

// Resolve frontend directory regardless of how the app is launched:
//   dotnet run  → ContentRoot = src/VoiceAssistLab.Api  → ../../frontend = repo root/frontend
//   ./run.sh    → AppContext.BaseDirectory = bin/Debug/net8.0/ → ../../../../frontend = repo root/frontend
var frontendPath = new[]
{
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "frontend"),
    Path.Combine(builder.Environment.ContentRootPath, "frontend"),
}
.Select(Path.GetFullPath)
.FirstOrDefault(Directory.Exists);

if (frontendPath is not null)
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
        RequestPath = "",
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendPath),
        RequestPath = "",
    });
}

// Endpoints
app.MapHealthEndpoints();
app.MapChatEndpoints();
app.MapVoiceEndpoints();

app.Run();

// Make the implicit Program class public for WebApplicationFactory in tests
public partial class Program { }
