using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.deleteFile",
    DisplayName = "Delete File",
    Category = NodeCategory.Action,
    Description = "Deletes a file from the file system",
    Color = "#22C55E")]
public class DeleteFileNode : IActionNode
{
    private string _filePath = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.deleteFile",
        DisplayName = "Delete File",
        Category = NodeCategory.Action,
        Description = "Deletes a file from the file system",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition>
        {
            new() { Id = "in", Name = "In", DataType = PortDataType.Flow }
        },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow }
        },
        Properties = new List<PropertyDefinition>
        {
            new()
            {
                Id = "filePath",
                Name = "File Path",
                Type = PropertyType.FilePath,
                DefaultValue = "",
                Description = "Path to the file to delete (supports {{variable}} templates)"
            }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        if (properties.TryGetValue("filePath", out var fp) && fp is string path)
            _filePath = path;
    }

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_filePath))
                return Task.FromResult(NodeResult.Fail("File path is required"));

            var resolvedPath = context.ResolveTemplate(_filePath);

            if (!File.Exists(resolvedPath))
                return Task.FromResult(NodeResult.Fail($"File not found: {resolvedPath}"));

            var fileName = Path.GetFileName(resolvedPath);
            File.Delete(resolvedPath);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["deletedFile"] = resolvedPath,
                ["fileName"] = fileName
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Failed to delete file: {ex.Message}"));
        }
    }
}
