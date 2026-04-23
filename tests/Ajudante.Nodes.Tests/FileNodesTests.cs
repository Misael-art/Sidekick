using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

public class FileNodesTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "AjudanteTests", Guid.NewGuid().ToString("N"));

    public FileNodesTests()
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
        var flow = new Flow { Name = "File Flow" };
        return new FlowExecutionContext(flow, CancellationToken.None);
    }

    [Fact]
    public async Task WriteFileNode_WritesResolvedContent()
    {
        var filePath = Path.Combine(_tempRoot, "output.txt");
        var node = new WriteFileNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["content"] = "Hello {{name}}",
            ["append"] = false
        });

        var context = CreateContext();
        context.SetVariable("name", "Alice");

        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(File.Exists(filePath));
        Assert.Equal("Hello Alice", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task WriteFileNode_AppendsWhenRequested()
    {
        var filePath = Path.Combine(_tempRoot, "append.txt");
        await File.WriteAllTextAsync(filePath, "A");

        var node = new WriteFileNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["content"] = "B",
            ["append"] = true
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("AB", await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task ReadFileNode_ReturnsContentAndStoresVariable()
    {
        var filePath = Path.Combine(_tempRoot, "read.txt");
        await File.WriteAllTextAsync(filePath, "sample-data");

        var node = new ReadFileNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["filePath"] = filePath,
            ["storeInVariable"] = "fileContent"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("sample-data", result.Outputs["content"]);
        Assert.Equal("sample-data", context.GetVariable("fileContent"));
    }

    [Fact]
    public async Task ListFilesNode_ReturnsMatchingFiles()
    {
        var folder = Path.Combine(_tempRoot, "list");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "a.txt"), "A");
        await File.WriteAllTextAsync(Path.Combine(folder, "b.log"), "B");

        var node = new ListFilesNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["folderPath"] = folder,
            ["pattern"] = "*.txt"
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        var files = Assert.IsType<string[]>(result.Outputs["files"]);
        Assert.Single(files);
        Assert.EndsWith("a.txt", files[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, result.Outputs["count"]);
    }

    [Fact]
    public async Task MoveFileNode_MovesFileToDestination()
    {
        var source = Path.Combine(_tempRoot, "source.txt");
        var destination = Path.Combine(_tempRoot, "nested", "destination.txt");
        await File.WriteAllTextAsync(source, "payload");

        var node = new MoveFileNode();
        node.Configure(new Dictionary<string, object?>
        {
            ["sourcePath"] = source,
            ["destinationPath"] = destination,
            ["overwrite"] = false
        });

        var context = CreateContext();
        var result = await node.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(destination));
        Assert.Equal("payload", await File.ReadAllTextAsync(destination));
    }
}
