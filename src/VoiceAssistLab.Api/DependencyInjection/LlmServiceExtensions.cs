using Anthropic.SDK;
using Microsoft.Extensions.Caching.Memory;
using VoiceAssistLab.Core.Llm;
using VoiceAssistLab.Infra.Cache;
using VoiceAssistLab.Infra.Llm;

namespace VoiceAssistLab.Api.DependencyInjection;

public static class LlmServiceExtensions
{
    public static IServiceCollection AddLlmClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();

        var provider = configuration["Llm:Provider"] ?? "groq";

        switch (provider.ToLowerInvariant())
        {
            case "groq":
                services.Configure<GroqOptions>(configuration.GetSection("Llm:Groq"));
                services.AddSingleton<ILlmClient>(sp =>
                {
                    var inner = new GroqLlmClient(
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GroqOptions>>());
                    return new CachingLlmClient(inner,
                        sp.GetRequiredService<IMemoryCache>());
                });
                break;

            case "anthropic":
                services.Configure<AnthropicOptions>(configuration.GetSection("Llm:Anthropic"));
                services.AddSingleton<ILlmClient>(sp =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>().Value;
                    var client = new AnthropicClient(opts.ApiKey);
                    var inner = new AnthropicLlmClient(client,
                        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnthropicOptions>>());
                    return new CachingLlmClient(inner,
                        sp.GetRequiredService<IMemoryCache>());
                });
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown LLM provider: '{provider}'. Valid values: 'groq', 'anthropic'.");
        }

        return services;
    }
}
