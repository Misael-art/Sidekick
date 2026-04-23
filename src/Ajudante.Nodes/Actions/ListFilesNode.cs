using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.listFiles",
    DisplayName = "List Files",
    Category = NodeCategory.Action,
    Description = "Lists files in a folder using a search pattern",
    Color = "#22C55E")]
public class ListFilesNode : IActionNode
{
    private string _folderPath = "";
    private string _pattern = "*.*";
    private bool _recursive;
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.listFiles",
        DisplayName = "List Files",
        Category = NodeCategory.Action,
        Description = "Lists files in a folder using a search pattern",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "files", Name = "Files", DataType = PortDataType.Any },
            new() { Id = "count", Name = "Count", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "folderPath", Name = "Folder Path", Type = PropertyType.FolderPath, DefaultValue = "", Description = "Folder to scan" },
            new() { Id = "pattern", Name = "Pattern", Type = PropertyType.String, DefaultValue = "*.*", Description = "Search pattern such as *.txt" },
            new() { Id = "recursive", Name = "Recursive", Type = PropertyType.Boolean, DefaultValue = false, Description = "Include subfolders" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the file list joined by new lines" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _folderPath = NodeValueHelper.GetString(properties, "folderPath");
        _pattern = NodeValueHelper.GetString(properties, "pattern", "*.*");
        _recursive = NodeValueHelper.GetBool(properties, "recursive");
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedFolder = context.ResolveTemplate(_folderPath);
        var resolvedPattern = context.ResolveTemplate(_pattern);

        if (string.IsNullOrWhiteSpace(resolvedFolder))
            return Task.FromResult(NodeResult.Fail("Folder path is required"));
        if (!Directory.Exists(resolvedFolder))
            return Task.FromResult(NodeResult.Fail($"Folder not found: {resolvedFolder}"));

        var files = Directory.GetFiles(
            resolvedFolder,
            string.IsNullOrWhiteSpace(resolvedPattern) ? "*.*" : resolvedPattern,
            _recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, string.Join(Environment.NewLine, files));

        return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["files"] = files,
            ["count"] = files.Length
        }));
    }
}
