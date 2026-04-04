using VoiceAssistLab.Core.Common;

namespace VoiceAssistLab.Core.Guardrails;

/// <summary>
/// Topic and size guardrail for user input.
/// Rejects off-scope topics and oversized audio payloads.
/// </summary>
public class InputGuardrailPipeline : IInputGuardrail
{
    // Keywords that indicate off-scope topics for an e-commerce support bot
    private static readonly string[] OffScopeKeywords =
    [
        "política", "esporte", "futebol", "receita", "música", "filme",
        "piada", "religião", "hack", "invasão", "crime",
        "política pública", "eleição", "governo",
    ];

    private const int MaxCharacters = 500;

    public Task<Result<string, string>> ValidateAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(Result<string, string>.Fail(
                "Desculpe, não consegui processar sua solicitação. Por favor, tente novamente."));

        if (input.Length > MaxCharacters)
            return Task.FromResult(Result<string, string>.Fail(
                "Sua mensagem é muito longa. Por favor, seja mais breve."));

        var lower = input.ToLowerInvariant();
        foreach (var keyword in OffScopeKeywords)
        {
            if (lower.Contains(keyword))
                return Task.FromResult(Result<string, string>.Fail(
                    "Posso apenas ajudar com pedidos, produtos e devoluções da nossa loja."));
        }

        return Task.FromResult(Result<string, string>.Ok(input));
    }
}
