using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.readFile",
    DisplayName = "Read File",
    Category = NodeCategory.Action,
    Description = "Reads text content from a file",
    Color = "#22C55E")]
public class ReadFileNode : IActionNode
{
    private string _filePath = "";
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.readFile",
        DisplayName = "Read File",
        Category = NodeCategory.Action,
        Description = "Reads text content from a file",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "content", Name = "Content", DataType = PortDataType.String },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Path to the file to read" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the file content" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _filePath = NodeValueHelper.GetString(properties, "filePath");
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedPath = context.ResolveTemplate(_filePath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return NodeResult.Fail("File path is required");
        if (!File.Exists(resolvedPath))
            return NodeResult.Fail($"File not found: {resolvedPath}");

        var content = await File.ReadAllTextAsync(resolvedPath, ct);
        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, content);

        return NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["content"] = content,
            ["filePath"] = resolvedPath
        });
    }
}
