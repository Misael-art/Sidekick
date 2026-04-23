using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.readCsv",
    DisplayName = "Read CSV",
    Category = NodeCategory.Action,
    Description = "Reads CSV rows from a file",
    Color = "#22C55E")]
public class ReadCsvNode : IActionNode
{
    private string _filePath = "";
    private string _delimiter = ",";
    private bool _hasHeaders = true;
    private string _storeInVariable = "";

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.readCsv",
        DisplayName = "Read CSV",
        Category = NodeCategory.Action,
        Description = "Reads CSV rows from a file",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "rows", Name = "Rows", DataType = PortDataType.Any },
            new() { Id = "count", Name = "Count", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "CSV file path" },
            new() { Id = "delimiter", Name = "Delimiter", Type = PropertyType.String, DefaultValue = ",", Description = "Field delimiter" },
            new() { Id = "hasHeaders", Name = "Has Headers", Type = PropertyType.Boolean, DefaultValue = true, Description = "Treat first row as headers" },
            new() { Id = "storeInVariable", Name = "Store In Variable", Type = PropertyType.String, DefaultValue = "", Description = "Optional variable to receive the parsed rows" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _filePath = NodeValueHelper.GetString(properties, "filePath");
        _delimiter = NodeValueHelper.GetString(properties, "delimiter", ",");
        _hasHeaders = NodeValueHelper.GetBool(properties, "hasHeaders", true);
        _storeInVariable = NodeValueHelper.GetString(properties, "storeInVariable");
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedPath = context.ResolveTemplate(_filePath);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return NodeResult.Fail("CSV file path is required");
        if (!File.Exists(resolvedPath))
            return NodeResult.Fail($"CSV file not found: {resolvedPath}");

        var content = await File.ReadAllTextAsync(resolvedPath, ct);
        var delimiter = string.IsNullOrEmpty(_delimiter) ? ',' : _delimiter[0];
        var rows = CsvUtility.ReadRows(content, delimiter, _hasHeaders);
        NodeValueHelper.SetVariableIfRequested(context, _storeInVariable, rows);

        return NodeResult.Ok("out", new Dictionary<string, object?>
        {
            ["rows"] = rows,
            ["count"] = rows.Count
        });
    }
}
