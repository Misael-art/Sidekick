using System.IO.Compression;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.archiveCreate",
    DisplayName = "Archive Create",
    Category = NodeCategory.Action,
    Description = "Create a zip/7z/tar.gz archive from files or a folder",
    Color = "#22C55E")]
public sealed class ArchiveCreateNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.archiveCreate",
        DisplayName = "Archive Create",
        Category = NodeCategory.Action,
        Description = "Create a zip (native), 7z or tar.gz (requires 7-Zip installed) archive.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "sourcePath", Name = "Source File or Folder", Type = PropertyType.String, DefaultValue = "", Description = "File path or folder. Multiple files: separate with ';'" },
            new() { Id = "outputFile", Name = "Output Archive Path", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "zip", Options = new[] { "zip", "7z", "tar.gz" } },
            new() { Id = "compressionLevel", Name = "Compression Level", Type = PropertyType.Dropdown, DefaultValue = "normal", Options = new[] { "store", "fast", "normal", "max" } },
            new() { Id = "password", Name = "Password (7z only)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "overwrite", Name = "Overwrite Existing", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var sourceRaw = context.ResolveTemplate(NodeValueHelper.GetString(_props, "sourcePath"));
            var output = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFile"));
            var format = NodeValueHelper.GetString(_props, "format", "zip").ToLowerInvariant();
            var levelKey = NodeValueHelper.GetString(_props, "compressionLevel", "normal").ToLowerInvariant();
            var password = NodeValueHelper.GetString(_props, "password");
            var overwrite = NodeValueHelper.GetBool(_props, "overwrite", false);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (string.IsNullOrWhiteSpace(sourceRaw) || string.IsNullOrWhiteSpace(output))
                return NodeResult.Fail("Source path and output file are required.");

            if (File.Exists(output))
            {
                if (!overwrite)
                    return NodeResult.Fail($"Output file already exists. Set Overwrite = true to replace: {output}");
                File.Delete(output);
            }

            var outputDir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDir))
                TempFileHelper.EnsureOutputFolder(outputDir);

            if (format == "zip")
            {
                var level = ResolveZipLevel(levelKey);
                CreateZipNative(sourceRaw, output, level);
            }
            else
            {
                var sevenZip = ToolDetector.FindSevenZip();
                if (sevenZip == null)
                    return NodeResult.Fail($"7-Zip is required for {format}. Install from https://www.7-zip.org/ and ensure 7z.exe is on PATH.");

                var typeArg = format == "7z" ? "7z" : "gzip";
                var lvlArg = levelKey switch
                {
                    "store" => "-mx=0",
                    "fast" => "-mx=1",
                    "max" => "-mx=9",
                    _ => "-mx=5"
                };
                var pwdArg = !string.IsNullOrEmpty(password) ? $" -p\"{password}\"" : "";

                if (format == "tar.gz")
                {
                    var tarPath = Path.ChangeExtension(output, ".tar");
                    if (File.Exists(tarPath)) File.Delete(tarPath);
                    var tarSpec = BuildSourceArgs(sourceRaw);
                    var tarOut = await ProcessRunner.RunAsync(sevenZip, $"a -ttar \"{tarPath}\" {tarSpec}", ct).ConfigureAwait(false);
                    if (tarOut.ExitCode != 0)
                        return NodeResult.Fail($"7z tar step failed (exit {tarOut.ExitCode}): {tarOut.StdErr}");
                    var gzOut = await ProcessRunner.RunAsync(sevenZip, $"a -tgzip {lvlArg} \"{output}\" \"{tarPath}\"", ct).ConfigureAwait(false);
                    File.Delete(tarPath);
                    if (gzOut.ExitCode != 0)
                        return NodeResult.Fail($"7z gzip step failed (exit {gzOut.ExitCode}): {gzOut.StdErr}");
                }
                else
                {
                    var spec = BuildSourceArgs(sourceRaw);
                    var args = $"a -t{typeArg} {lvlArg}{pwdArg} \"{output}\" {spec}";
                    var outcome = await ProcessRunner.RunAsync(sevenZip, args, ct).ConfigureAwait(false);
                    if (outcome.ExitCode != 0)
                        return NodeResult.Fail($"7z create failed (exit {outcome.ExitCode}): {outcome.StdErr}");
                }
            }

            var size = new FileInfo(output).Length;
            var fileCount = CountSourceFiles(sourceRaw);
            NodeValueHelper.SetVariableIfRequested(context, outVar, output);

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = output,
                ["fileCount"] = fileCount,
                ["sizeBytes"] = size
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Archive create failed: {ex.Message}");
        }
    }

    private static CompressionLevel ResolveZipLevel(string key) => key switch
    {
        "store" => CompressionLevel.NoCompression,
        "fast" => CompressionLevel.Fastest,
        "max" => CompressionLevel.SmallestSize,
        _ => CompressionLevel.Optimal
    };

    private static void CreateZipNative(string source, string output, CompressionLevel level)
    {
        var sources = source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sources.Length == 1 && Directory.Exists(sources[0]))
        {
            ZipFile.CreateFromDirectory(sources[0], output, level, includeBaseDirectory: false);
            return;
        }

        using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
        foreach (var path in sources)
        {
            if (File.Exists(path))
            {
                zip.CreateEntryFromFile(path, Path.GetFileName(path), level);
            }
            else if (Directory.Exists(path))
            {
                var baseDir = path;
                foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    var entry = Path.GetRelativePath(baseDir, f).Replace('\\', '/');
                    zip.CreateEntryFromFile(f, entry, level);
                }
            }
        }
    }

    private static string BuildSourceArgs(string source)
    {
        var sources = source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', sources.Select(s => $"\"{s}\""));
    }

    private static int CountSourceFiles(string source)
    {
        var sources = source.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var count = 0;
        foreach (var s in sources)
        {
            if (File.Exists(s)) count++;
            else if (Directory.Exists(s)) count += Directory.GetFiles(s, "*", SearchOption.AllDirectories).Length;
        }
        return count;
    }
}

[NodeInfo(
    TypeId = "action.archiveExtract",
    DisplayName = "Archive Extract",
    Category = NodeCategory.Action,
    Description = "Extract zip/7z/tar.gz archive into a folder",
    Color = "#22C55E")]
public sealed class ArchiveExtractNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.archiveExtract",
        DisplayName = "Archive Extract",
        Category = NodeCategory.Action,
        Description = "Extract a zip (native), 7z or tar.gz (requires 7-Zip installed) archive.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input Archive", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "overwrite", Name = "Overwrite Existing Files", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "password", Name = "Password (7z only)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "outputVariable", Name = "Output Folder Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var output = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var overwrite = NodeValueHelper.GetBool(_props, "overwrite", false);
            var password = NodeValueHelper.GetString(_props, "password");
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input))
                return NodeResult.Fail($"Archive not found: {input}");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Path.GetDirectoryName(input) ?? Path.GetTempPath(), Path.GetFileNameWithoutExtension(input));

            TempFileHelper.EnsureOutputFolder(output);

            var ext = Path.GetExtension(input).ToLowerInvariant();
            var lower = input.ToLowerInvariant();
            int fileCount = 0;

            if (ext == ".zip")
            {
                using var zip = ZipFile.OpenRead(input);
                fileCount = zip.Entries.Count;
                foreach (var entry in zip.Entries)
                {
                    var dest = Path.Combine(output, entry.FullName);
                    var destDir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);
                    if (string.IsNullOrEmpty(entry.Name))
                        continue;
                    entry.ExtractToFile(dest, overwrite);
                }
            }
            else if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz") || ext == ".7z" || ext == ".gz" || ext == ".tar" || ext == ".rar")
            {
                var sevenZip = ToolDetector.FindSevenZip();
                if (sevenZip == null)
                    return NodeResult.Fail("7-Zip is required to extract this format. Install from https://www.7-zip.org/ and ensure 7z.exe is on PATH.");

                var pwdArg = !string.IsNullOrEmpty(password) ? $" -p\"{password}\"" : "";
                var owArg = overwrite ? " -aoa" : " -aos";
                var args = $"x \"{input}\" -o\"{output}\"{pwdArg}{owArg} -y";
                var outcome = await ProcessRunner.RunAsync(sevenZip, args, ct).ConfigureAwait(false);
                if (outcome.ExitCode != 0)
                    return NodeResult.Fail($"7z extract failed (exit {outcome.ExitCode}): {outcome.StdErr}");
                fileCount = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Length;
            }
            else
            {
                return NodeResult.Fail($"Unsupported archive extension: {ext}");
            }

            NodeValueHelper.SetVariableIfRequested(context, outVar, output);

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputFolder"] = output,
                ["fileCount"] = fileCount
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Archive extract failed: {ex.Message}");
        }
    }
}
