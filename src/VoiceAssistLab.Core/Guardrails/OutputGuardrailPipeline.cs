using System.Text.RegularExpressions;
using VoiceAssistLab.Core.Common;

namespace VoiceAssistLab.Core.Guardrails;

/// <summary>
/// Output guardrail: checks language plausibility and order ID data integrity.
/// </summary>
public partial class OutputGuardrailPipeline : IOutputGuardrail
{
    // Common Portuguese stop words — a response missing all of these is likely not PT-BR
    private static readonly string[] PortugueseStopWords =
        ["o", "a", "os", "as", "de", "da", "do", "em", "e", "é", "que", "para", "com", "não", "um", "uma"];

    // Order IDs exposed in mock data — none other should appear in output
    private static readonly HashSet<string> KnownOrderIds =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "ORD-001", "ORD-002", "ORD-003", "ORD-004", "ORD-005",
        };

    private static readonly Regex OrderIdPattern = OrderIdRegex();

    public Task<Result<string, string>> ValidateAsync(string output, string originalInput, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(output))
            return Task.FromResult(Result<string, string>.Fail(
                "Desculpe, não consegui processar sua solicitação. Por favor, tente novamente."));

        // Portuguese language heuristic
        var lower = output.ToLowerInvariant();
        var hasPortuguese = PortugueseStopWords.Any(w => lower.Contains($" {w} ") || lower.StartsWith($"{w} "));
        if (!hasPortuguese && output.Length > 30)
            return Task.FromResult(Result<string, string>.Fail(
                "Desculpe, não consegui processar sua solicitação. Por favor, tente novamente."));

        // Order ID integrity: any ORD-xxx in output must be a known order
        foreach (Match match in OrderIdPattern.Matches(output))
        {
            if (!KnownOrderIds.Contains(match.Value))
                return Task.FromResult(Result<string, string>.Fail(
                    "Desculpe, não consegui processar sua solicitação. Por favor, tente novamente."));
        }

        return Task.FromResult(Result<string, string>.Ok(output));
    }

    [GeneratedRegex(@"ORD-\d+", RegexOptions.IgnoreCase)]
    private static partial Regex OrderIdRegex();
}
