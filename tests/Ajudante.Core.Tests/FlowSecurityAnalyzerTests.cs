using Ajudante.Core;
using Ajudante.Core.Engine;

namespace Ajudante.Core.Tests;

public sealed class FlowSecurityAnalyzerTests
{
    [Fact]
    public void ComputeSecurityManifestHash_matches_analyze_output_for_same_flow()
    {
        var flow = new Flow
        {
            Id = "f1",
            Name = "Test",
            Nodes =
            [
                new NodeInstance { Id = "n1", TypeId = "action.deleteFile" },
            ],
        };

        var analyzer = new FlowSecurityAnalyzer();
        var report = analyzer.Analyze(flow);
        var recomputed = FlowSecurityAnalyzer.ComputeSecurityManifestHash(flow, report.Issues);

        Assert.Equal(report.ManifestHash, recomputed, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComputeSecurityManifestHash_changes_when_issue_codes_change()
    {
        var flow = new Flow
        {
            Id = "f1",
            Name = "Test",
            Nodes = [new NodeInstance { Id = "n1", TypeId = "action.mouseClick" }],
        };

        var issuesA = new List<SecurityIssue>
        {
            new()
            {
                Code = "security.globalInput",
                Severity = SecuritySeverity.Info,
                Message = "m1",
                NodeId = "n1",
            },
        };

        var issuesB = new List<SecurityIssue>
        {
            new()
            {
                Code = "security.networkOrExfiltration",
                Severity = SecuritySeverity.Warning,
                Message = "m2",
                NodeId = "n1",
            },
        };

        var hashA = FlowSecurityAnalyzer.ComputeSecurityManifestHash(flow, issuesA);
        var hashB = FlowSecurityAnalyzer.ComputeSecurityManifestHash(flow, issuesB);

        Assert.NotEqual(hashA, hashB, StringComparer.OrdinalIgnoreCase);
    }
}
