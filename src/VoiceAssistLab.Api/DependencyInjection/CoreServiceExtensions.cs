using FluentValidation;
using VoiceAssistLab.Core.Chat;
using VoiceAssistLab.Core.Guardrails;
using VoiceAssistLab.Core.Tools;
using VoiceAssistLab.Infra.MockData;

namespace VoiceAssistLab.Api.DependencyInjection;

public static class CoreServiceExtensions
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Mock data
        services.AddSingleton<MockDataRepository>();

        // Tool executors
        services.AddSingleton<IToolExecutor, GetOrderStatusTool>();
        services.AddSingleton<IToolExecutor, GetProductInfoTool>();
        services.AddSingleton<IToolExecutor, GetReturnPolicyTool>();
        services.AddSingleton<ToolRegistry>();

        // Chat
        services.AddScoped<IChatOrchestrator, ChatOrchestrator>();

        // Guardrails
        services.AddSingleton<IInputGuardrail, InputGuardrailPipeline>();
        services.AddSingleton<IOutputGuardrail, OutputGuardrailPipeline>();

        // Validation
        services.AddValidatorsFromAssemblyContaining<ChatRequestValidator>(ServiceLifetime.Singleton);

        return services;
    }
}
