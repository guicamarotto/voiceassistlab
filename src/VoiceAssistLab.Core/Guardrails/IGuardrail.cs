using VoiceAssistLab.Core.Common;

namespace VoiceAssistLab.Core.Guardrails;

public interface IInputGuardrail
{
    Task<Result<string, string>> ValidateAsync(string input, CancellationToken ct = default);
}

public interface IOutputGuardrail
{
    Task<Result<string, string>> ValidateAsync(string output, string originalInput, CancellationToken ct = default);
}
