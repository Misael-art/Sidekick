using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.writeFile",
    DisplayName = "Write File",
    Category = NodeCategory.Action,
    Description = "Writes text content to a file",
    Color = "#22C55E")]
public class WriteFileNode : IActionNode
{
    private string _filePath = "";
    private string _content = "";
    private bool _append;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.writeFile",
        DisplayName = "Write File",
        Category = NodeCategory.Action,
        Description = "Writes text content to a file",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Path to the destination file" },
            new() { Id = "content", Name = "Content", Type = PropertyType.String, DefaultValue = "", Description = "Text to write (supports {{variable}} templates)" },
            new() { Id = "append", Name = "Append", Type = PropertyType.Boolean, DefaultValue = false, Description = "Append instead of overwriting" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _filePath = NodeValueHelper.GetString(properties, "filePath");
        _content = NodeValueHelper.GetString(properties, "content");
        _append = NodeValueHelper.GetBool(properties, "append");
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedPath = context.ResolveTemplate(_filePath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return NodeResult.Fail("File path is required");

        var resolvedContent = context.ResolveTemplate(_content);
        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (_append)
            await File.AppendAllTextAsync(resolvedPath, resolvedContent, ct);
        else
            await File.WriteAllTextAsync(resolvedPath, resolvedContent, ct);

        return NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["filePath"] = resolvedPath,
            ["contentLength"] = resolvedContent.Length
        });
    }
}
