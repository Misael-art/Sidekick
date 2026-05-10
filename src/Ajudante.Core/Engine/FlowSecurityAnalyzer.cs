using Ajudante.Core;

namespace Ajudante.Core.Engine;

public enum SecuritySeverity
{
    Info,
    Warning,
    Block
}

public sealed class SecurityIssue
{
    public required string Code { get; set; }
    public required SecuritySeverity Severity { get; set; }
    public required string Message { get; set; }
    public string? NodeId { get; set; }
}

public sealed class SecurityReport
{
    public bool IsSafeToRun { get; set; } = true;
    public List<SecurityIssue> Issues { get; set; } = [];
    public string RiskLevel { get; set; } = "low";
    public string ManifestHash { get; set; } = "";
}

/// <summary>
/// Lightweight static analyzer that flags potentially sensitive flows before run/export.
/// It is intentionally conservative and explainable to keep audits simple.
/// </summary>
public sealed class FlowSecurityAnalyzer
{
    public SecurityReport Analyze(Flow flow)
    {
        var report = new SecurityReport();
        var issues = new List<SecurityIssue>();

        foreach (var node in flow.Nodes)
        {
            var typeId = node.TypeId ?? string.Empty;

            if (typeId.Contains("delete", StringComparison.OrdinalIgnoreCase)
                || typeId.Contains("killProcess", StringComparison.OrdinalIgnoreCase)
                || typeId.Contains("systemPower", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Code = "security.destructiveAction",
                    Severity = SecuritySeverity.Block,
                    NodeId = node.Id,
                    Message = $"Node '{node.Id}' executa acao destrutiva ({typeId}) e exige atencao explicita."
                });
            }

            if (typeId.Contains("download", StringComparison.OrdinalIgnoreCase)
                || typeId.Contains("httpRequest", StringComparison.OrdinalIgnoreCase)
                || typeId.Contains("sendEmail", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Code = "security.networkOrExfiltration",
                    Severity = SecuritySeverity.Warning,
                    NodeId = node.Id,
                    Message = $"Node '{node.Id}' usa rede/comunicacao externa ({typeId}). Revise dados enviados."
                });
            }

            if (typeId.Contains("keyboard", StringComparison.OrdinalIgnoreCase)
                || typeId.Contains("mouse", StringComparison.OrdinalIgnoreCase)
                || typeId.Contains("desktopClickElement", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new SecurityIssue
                {
                    Code = "security.globalInput",
                    Severity = SecuritySeverity.Info,
                    NodeId = node.Id,
                    Message = $"Node '{node.Id}' injeta input global ({typeId}). Execute com janela alvo sob supervisao."
                });
            }
        }

        report.Issues = issues
            .GroupBy(issue => $"{issue.Code}:{issue.NodeId}:{issue.Message}")
            .Select(group => group.First())
            .ToList();

        report.IsSafeToRun = report.Issues.All(issue => issue.Severity != SecuritySeverity.Block);
        report.RiskLevel = report.Issues.Any(issue => issue.Severity == SecuritySeverity.Block)
            ? "high"
            : report.Issues.Any(issue => issue.Severity == SecuritySeverity.Warning)
                ? "medium"
                : "low";
        report.ManifestHash = ComputeSecurityManifestHash(flow, report.Issues);
        return report;
    }

    /// <summary>
    /// Deterministic fingerprint for a flow plus security issue codes (same inputs as <see cref="Analyze"/> uses for <see cref="SecurityReport.ManifestHash"/>).
    /// Callers (host app, export runner) must use this API to avoid drift with the analyzer.
    /// </summary>
    public static string ComputeSecurityManifestHash(Flow flow, IEnumerable<SecurityIssue> issues)
    {
        var issueList = issues as IReadOnlyList<SecurityIssue> ?? issues.ToList();
        var payload = $"{flow.Id}|{flow.Name}|{string.Join("|", flow.Nodes.Select(n => n.TypeId))}|{string.Join("|", issueList.Select(i => i.Code))}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

