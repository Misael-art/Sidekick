using System.Text.Json;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.writeCsv",
    DisplayName = "Write CSV",
    Category = NodeCategory.Action,
    Description = "Writes CSV content from JSON row data",
    Color = "#22C55E")]
public class WriteCsvNode : IActionNode
{
    private string _filePath = "";
    private string _rowsJson = "";
    private string _delimiter = ",";
    private bool _includeHeaders = true;

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.writeCsv",
        DisplayName = "Write CSV",
        Category = NodeCategory.Action,
        Description = "Writes CSV content from JSON row data",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Out", DataType = PortDataType.Flow },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String },
            new() { Id = "rowCount", Name = "Row Count", DataType = PortDataType.Number }
        },
        Properties = new List<PropertyDefinition>
        {
            new() { Id = "filePath", Name = "File Path", Type = PropertyType.FilePath, DefaultValue = "", Description = "Target CSV file" },
            new() { Id = "rowsJson", Name = "Rows JSON", Type = PropertyType.String, DefaultValue = "[]", Description = "JSON array of objects to serialize as CSV" },
            new() { Id = "delimiter", Name = "Delimiter", Type = PropertyType.String, DefaultValue = ",", Description = "Field delimiter" },
            new() { Id = "includeHeaders", Name = "Include Headers", Type = PropertyType.Boolean, DefaultValue = true, Description = "Write a header row" }
        }
    };

    public void Configure(Dictionary<string, object?> properties)
    {
        _filePath = NodeValueHelper.GetString(properties, "filePath");
        _rowsJson = NodeValueHelper.GetString(properties, "rowsJson", "[]");
        _delimiter = NodeValueHelper.GetString(properties, "delimiter", ",");
        _includeHeaders = NodeValueHelper.GetBool(properties, "includeHeaders", true);
    }

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        var resolvedPath = context.ResolveTemplate(_filePath);
        var resolvedRowsJson = context.ResolveTemplate(_rowsJson);
        if (string.IsNullOrWhiteSpace(resolvedPath))
            return NodeResult.Fail("CSV file path is required");

        try
        {
            var rows = ParseRows(resolvedRowsJson);
            var delimiter = string.IsNullOrEmpty(_delimiter) ? ',' : _delimiter[0];
            var csv = CsvUtility.WriteRows(rows, delimiter, _includeHeaders);

            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(resolvedPath, csv, ct);

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["filePath"] = resolvedPath,
                ["rowCount"] = rows.Count
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail(ex.Message);
        }
    }

    private static List<Dictionary<string, string>> ParseRows(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Rows JSON must be an array");

        var rows = new List<Dictionary<string, string>>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in item.EnumerateObject())
            {
                row[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString();
            }

            rows.Add(row);
        }

        return rows;
    }
}
