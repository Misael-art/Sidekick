using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.moveFile",
    DisplayName = "Move / Rename File",
    Category = NodeCategory.Action,
    Description = "Moves or renames a file",
    Color = "#22C55E")]
public class MoveFileNode : IActionNode
{
    private string _sourcePath = "";
    private string _destinationPath = "";
    private bool _overwrite;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.moveFile",
        DisplayName = "Move / Rename File",
        Category = NodeCategory.Action,
        Description = "Moves or renames a file",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "destinationPath", Name = "Destination Path", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "sourcePath", Name = "Source Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Path to the source file" },
            new() { Id = "destinationPath", Name = "Destination Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Path to the destination file" },
            new() { Id = "overwrite", Name = "Overwrite", Type = PropertyType.Boolean, DefaultValue = false, Description = "Overwrite if destination already exists" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _sourcePath = NodeValueHelper.GetString(properties, "sourcePath");
        _destinationPath = NodeValueHelper.GetString(properties, "destinationPath");
        _overwrite = NodeValueHelper.GetBool(properties, "overwrite");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedSource = context.ResolveTemplate(_sourcePath);
        var resolvedDestination = context.ResolveTemplate(_destinationPath);

        if (string.IsNullOrWhiteSpace(resolvedSource) || string.IsNullOrWhiteSpace(resolvedDestination))
            return Task.FromResult(NodeResult.Fail("Source and destination paths are required"));
        if (!File.Exists(resolvedSource))
            return Task.FromResult(NodeResult.Fail($"Source file not found: {resolvedSource}"));

        var destinationDirectory = Path.GetDirectoryName(resolvedDestination);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        if (_overwrite && File.Exists(resolvedDestination))
            File.Delete(resolvedDestination);

        File.Move(resolvedSource, resolvedDestination);

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["destinationPath"] = resolvedDestination,
            ["fileName"] = Path.GetFileName(resolvedDestination)
        }));
    }
}
