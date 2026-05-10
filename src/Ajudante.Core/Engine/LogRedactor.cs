using System.Text.RegularExpressions;

namespace Ajudante.Core.Engine;

public static class LogRedactor
{
    private static readonly Regex KeyValueSecretRegex = new(
        @"(?i)\b(password|passwd|pwd|token|api[_-]?key|secret|client[_-]?secret)\b(\s*[:=]\s*)([^\s;,&]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AuthorizationBearerRegex = new(
        @"(?i)\b(authorization\s*:\s*bearer\s+)([^\s;,&]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OpenAiKeyRegex = new(
        @"\bsk-(?:proj-|live-)?[A-Za-z0-9_-]{6,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message ?? string.Empty;
        }

        var redacted = KeyValueSecretRegex.Replace(message, "$1$2***");
        redacted = AuthorizationBearerRegex.Replace(redacted, "$1***");
        redacted = OpenAiKeyRegex.Replace(redacted, "sk-***");
        return redacted;
    }
}
