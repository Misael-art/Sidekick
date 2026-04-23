using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;
using Ajudante.Nodes.Logic;

namespace Ajudante.Nodes.Tests;

public class DataNodesTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "AjudanteDataTests", Guid.NewGuid().ToString("N"));

    public DataNodesTests()
    {
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static FlowExecutionContext CreateContext()
    {
        var flow = new Flow { Name = "Data Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public async Task JsonExtractNode_ReadsNestedValue()
    {
        var node = new JsonExtractNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["json"] = "{\"data\":{\"items\":[{\"name\":\"Alice\"}]}}",
            ["path"] = "data.items[0].name",
            ["storeInVariable"] = "customerName"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Alice", result.Outputs["value"]);
        Assert.Equal("Alice", context.GetVariable("customerName"));
    }

    [Fact]
    public async Task FilterTextLinesNode_FiltersMatchingLines()
    {
        var node = new FilterTextLinesNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["input"] = "report-1.txt\nerror.log\nreport-2.txt",
            ["pattern"] = "report",
            ["mode"] = "startsWith",
            ["storeInVariable"] = "filtered"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Outputs["count"]);
        Assert.Equal("report-1.txt" + Environment.NewLine + "report-2.txt", context.GetVariable("filtered"));
    }

    [Fact]
    public async Task ReadCsvNode_ParsesRowsWithHeaders()
    {
        var path = Path.Combine(_tempRoot, "input.csv");
        await File.WriteAllTextAsync(path, "Name,Age\nAlice,30\nBob,25");

        var node = new ReadCsvNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["filePath"] = path,
            ["delimiter"] = ",",
            ["hasHeaders"] = true,
            ["storeInVariable"] = "rows"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        var rows = Assert.IsType<List<Dictionary<string, string>>>(result.Outputs["rows"]);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Alice", rows[0]["Name"]);
        Assert.Equal("25", rows[1]["Age"]);
        Assert.NotNull(context.GetVariable("rows"));
    }

    [Fact]
    public async Task WriteCsvNode_WritesRowsFromJson()
    {
        var path = Path.Combine(_tempRoot, "output.csv");
        var node = new WriteCsvNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["filePath"] = path,
            ["rowsJson"] = "[{\"Name\":\"Alice\",\"Age\":\"30\"},{\"Name\":\"Bob\",\"Age\":\"25\"}]",
            ["includeHeaders"] = true
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.Outputs["rowCount"]);
        var content = await File.ReadAllTextAsync(path);
        Assert.Contains("Name,Age", content);
        Assert.Contains("Alice,30", content);
        Assert.Contains("Bob,25", content);
    }
}
