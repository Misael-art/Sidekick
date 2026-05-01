using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.fileHash",
    DisplayName = "File Hash",
    Category = NodeCategory.Action,
    Description = "Compute MD5/SHA1/SHA256 hash of a file",
    Color = "#22C55E")]
public sealed class FileHashNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.fileHash",
        DisplayName = "File Hash",
        Category = NodeCategory.Action,
        Description = "Compute MD5, SHA1 or SHA256 of a file. Hex string output.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "algorithm", Name = "Algorithm", Type = PropertyType.Dropdown, DefaultValue = "SHA256", Options = new[] { "MD5", "SHA1", "SHA256" } },
            new() { Id = "outputVariable", Name = "Hash Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var algo = NodeValueHelper.GetString(_props, "algorithm", "SHA256");
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input))
                return Task.FromResult(NodeResult.Fail($"Input file not found: {input}"));

            var hash = ComputeHash(input, algo);
            NodeValueHelper.SetVariableIfRequested(context, outVar, hash);
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["hash"] = hash,
                ["algorithm"] = algo
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"File hash failed: {ex.Message}"));
        }
    }

    public static string ComputeHash(string path, string algorithm)
    {
        using HashAlgorithm h = algorithm.ToUpperInvariant() switch
        {
            "MD5" => MD5.Create(),
            "SHA1" => SHA1.Create(),
            _ => SHA256.Create()
        };
        using var s = File.OpenRead(path);
        var bytes = h.ComputeHash(s);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

[NodeInfo(
    TypeId = "action.findDuplicateFiles",
    DisplayName = "Find Duplicate Files",
    Category = NodeCategory.Action,
    Description = "Find duplicate files in a folder by size and hash",
    Color = "#22C55E")]
public sealed class FindDuplicateFilesNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.findDuplicateFiles",
        DisplayName = "Find Duplicate Files",
        Category = NodeCategory.Action,
        Description = "Group duplicates by size first, then SHA256 hash. Read-only — never deletes.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "folder", Name = "Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filter", Name = "Filter", Type = PropertyType.String, DefaultValue = "*.*" },
            new() { Id = "recursive", Name = "Recursive", Type = PropertyType.Boolean, DefaultValue = true },
            new() { Id = "minSizeBytes", Name = "Min Size (bytes)", Type = PropertyType.Integer, DefaultValue = 1 },
            new() { Id = "outputVariable", Name = "Result Variable (JSON)", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "folder"));
            var filter = NodeValueHelper.GetString(_props, "filter", "*.*");
            var recursive = NodeValueHelper.GetBool(_props, "recursive", true);
            var minSize = NodeValueHelper.GetInt(_props, "minSizeBytes", 1);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!Directory.Exists(folder))
                return Task.FromResult(NodeResult.Fail($"Folder not found: {folder}"));

            var files = Directory.GetFiles(folder, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(p => new FileInfo(p))
                .Where(f => f.Length >= minSize)
                .ToList();

            var duplicates = new List<List<string>>();
            var bySize = files.GroupBy(f => f.Length).Where(g => g.Count() > 1);
            foreach (var sizeGroup in bySize)
            {
                ct.ThrowIfCancellationRequested();
                var byHash = sizeGroup.GroupBy(f => FileHashNode.ComputeHash(f.FullName, "SHA256"))
                                      .Where(g => g.Count() > 1);
                foreach (var hashGroup in byHash)
                {
                    duplicates.Add(hashGroup.Select(f => f.FullName).ToList());
                }
            }

            var json = JsonSerializer.Serialize(duplicates);
            NodeValueHelper.SetVariableIfRequested(context, outVar, json);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["duplicateGroups"] = duplicates,
                ["groupCount"] = duplicates.Count,
                ["duplicateFileCount"] = duplicates.Sum(g => g.Count)
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Duplicate scan failed: {ex.Message}"));
        }
    }
}

[NodeInfo(
    TypeId = "action.cleanFolder",
    DisplayName = "Clean Folder",
    Category = NodeCategory.Action,
    Description = "Delete or move old files. Dry-run by default.",
    Color = "#22C55E")]
public sealed class CleanFolderNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.cleanFolder",
        DisplayName = "Clean Folder",
        Category = NodeCategory.Action,
        Description = "Identify and (optionally) delete or move files older than N days. Default dryRun=true.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "folder", Name = "Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filter", Name = "Filter", Type = PropertyType.String, DefaultValue = "*.*" },
            new() { Id = "recursive", Name = "Recursive", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "olderThanDays", Name = "Older Than (days)", Type = PropertyType.Integer, DefaultValue = 30 },
            new() { Id = "action", Name = "Action", Type = PropertyType.Dropdown, DefaultValue = "delete", Options = new[] { "delete", "move" } },
            new() { Id = "moveDestination", Name = "Move Destination", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true },
            new() { Id = "confirmApply", Name = "Confirm Apply (required when dryRun=false)", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "outputVariable", Name = "Result Variable (JSON)", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "folder"));
            var filter = NodeValueHelper.GetString(_props, "filter", "*.*");
            var recursive = NodeValueHelper.GetBool(_props, "recursive", false);
            var days = Math.Max(0, NodeValueHelper.GetInt(_props, "olderThanDays", 30));
            var action = NodeValueHelper.GetString(_props, "action", "delete").ToLowerInvariant();
            var moveDest = context.ResolveTemplate(NodeValueHelper.GetString(_props, "moveDestination"));
            var dryRun = NodeValueHelper.GetBool(_props, "dryRun", true);
            var confirm = NodeValueHelper.GetBool(_props, "confirmApply", false);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!Directory.Exists(folder))
                return Task.FromResult(NodeResult.Fail($"Folder not found: {folder}"));

            var threshold = DateTime.UtcNow - TimeSpan.FromDays(days);
            var candidates = Directory.GetFiles(folder, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Select(p => new FileInfo(p))
                .Where(f => f.LastWriteTimeUtc < threshold)
                .Select(f => f.FullName)
                .ToList();

            var processed = 0;
            var errors = new List<string>();
            if (!dryRun)
            {
                if (!confirm)
                    return Task.FromResult(NodeResult.Fail("Refusing destructive action without confirmApply=true."));

                if (action == "move")
                {
                    if (string.IsNullOrWhiteSpace(moveDest))
                        return Task.FromResult(NodeResult.Fail("Move action requires a destination folder."));
                    TempFileHelper.EnsureOutputFolder(moveDest);
                }

                foreach (var path in candidates)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (action == "move")
                        {
                            var dst = TempFileHelper.EnsureUnique(Path.Combine(moveDest, Path.GetFileName(path)));
                            File.Move(path, dst);
                        }
                        else
                        {
                            File.Delete(path);
                        }
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{path}: {ex.Message}");
                    }
                }
            }

            var preview = JsonSerializer.Serialize(candidates);
            NodeValueHelper.SetVariableIfRequested(context, outVar, preview);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["dryRun"] = dryRun,
                ["candidateCount"] = candidates.Count,
                ["processedCount"] = processed,
                ["errorsJson"] = JsonSerializer.Serialize(errors),
                ["previewJson"] = preview
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Clean folder failed: {ex.Message}"));
        }
    }
}

[NodeInfo(
    TypeId = "action.organizeFolder",
    DisplayName = "Organize Folder",
    Category = NodeCategory.Action,
    Description = "Sort files into subfolders by extension/date/type. Dry-run by default.",
    Color = "#22C55E")]
public sealed class OrganizeFolderNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.organizeFolder",
        DisplayName = "Organize Folder",
        Category = NodeCategory.Action,
        Description = "Move files into subfolders grouped by extension, date (yyyy-MM) or type. Default dryRun=true.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "folder", Name = "Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "groupBy", Name = "Group By", Type = PropertyType.Dropdown, DefaultValue = "extension", Options = new[] { "extension", "date", "type" } },
            new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true },
            new() { Id = "confirmApply", Name = "Confirm Apply", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "outputVariable", Name = "Plan Variable (JSON)", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "folder"));
            var groupBy = NodeValueHelper.GetString(_props, "groupBy", "extension").ToLowerInvariant();
            var dryRun = NodeValueHelper.GetBool(_props, "dryRun", true);
            var confirm = NodeValueHelper.GetBool(_props, "confirmApply", false);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!Directory.Exists(folder))
                return Task.FromResult(NodeResult.Fail($"Folder not found: {folder}"));

            var plan = new List<(string Source, string Destination)>();
            foreach (var path in Directory.GetFiles(folder))
            {
                var subFolder = ResolveBucket(path, groupBy);
                var dst = Path.Combine(folder, subFolder, Path.GetFileName(path));
                plan.Add((path, dst));
            }

            var processed = 0;
            var errors = new List<string>();
            if (!dryRun)
            {
                if (!confirm)
                    return Task.FromResult(NodeResult.Fail("Refusing to move files without confirmApply=true."));

                foreach (var entry in plan)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var dstDir = Path.GetDirectoryName(entry.Destination);
                        if (!string.IsNullOrEmpty(dstDir))
                            Directory.CreateDirectory(dstDir);
                        var dst = TempFileHelper.EnsureUnique(entry.Destination);
                        File.Move(entry.Source, dst);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{entry.Source}: {ex.Message}");
                    }
                }
            }

            var planJson = JsonSerializer.Serialize(plan.Select(p => new { source = p.Source, destination = p.Destination }));
            NodeValueHelper.SetVariableIfRequested(context, outVar, planJson);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["dryRun"] = dryRun,
                ["plannedCount"] = plan.Count,
                ["processedCount"] = processed,
                ["errorsJson"] = JsonSerializer.Serialize(errors),
                ["planJson"] = planJson
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Organize folder failed: {ex.Message}"));
        }
    }

    private static string ResolveBucket(string path, string groupBy)
    {
        return groupBy switch
        {
            "date" => File.GetLastWriteTime(path).ToString("yyyy-MM", CultureInfo.InvariantCulture),
            "type" => ClassifyType(Path.GetExtension(path).ToLowerInvariant()),
            _ => string.IsNullOrEmpty(Path.GetExtension(path)) ? "no_extension" : Path.GetExtension(path).TrimStart('.').ToLowerInvariant()
        };
    }

    private static string ClassifyType(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tiff" or ".webp" => "images",
        ".mp4" or ".mov" or ".mkv" or ".avi" or ".webm" => "videos",
        ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "audio",
        ".pdf" or ".doc" or ".docx" or ".odt" or ".rtf" or ".txt" or ".md" => "documents",
        ".zip" or ".7z" or ".tar" or ".gz" or ".rar" => "archives",
        ".xls" or ".xlsx" or ".csv" or ".ods" => "spreadsheets",
        _ => "other"
    };
}

[NodeInfo(
    TypeId = "action.batchRename",
    DisplayName = "Batch Rename",
    Category = NodeCategory.Action,
    Description = "Rename files in bulk with several modes. Dry-run by default.",
    Color = "#22C55E")]
public sealed class BatchRenameNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.batchRename",
        DisplayName = "Batch Rename",
        Category = NodeCategory.Action,
        Description = "Rename files in bulk. Modes: prefix, suffix, replace, regexReplace, sequence, datePattern, template. Default dryRun=true.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "folder", Name = "Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filter", Name = "Filter", Type = PropertyType.String, DefaultValue = "*.*" },
            new() { Id = "recursive", Name = "Recursive", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "prefix",
                Options = new[] { "prefix", "suffix", "replace", "regexReplace", "sequence", "datePattern", "template" } },
            new() { Id = "value", Name = "Value (prefix/suffix/from)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "replacement", Name = "Replacement", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "template", Name = "Template", Type = PropertyType.String, DefaultValue = "{{name}}{{ext}}",
                Description = "Tokens: {{name}}, {{ext}}, {{index}}, {{date}}, {{createdAt}}, {{modifiedAt}}" },
            new() { Id = "startIndex", Name = "Start Index", Type = PropertyType.Integer, DefaultValue = 1 },
            new() { Id = "indexPad", Name = "Index Padding", Type = PropertyType.Integer, DefaultValue = 3 },
            new() { Id = "datePattern", Name = "Date Pattern", Type = PropertyType.String, DefaultValue = "yyyyMMdd" },
            new() { Id = "dryRun", Name = "Dry Run", Type = PropertyType.Boolean, DefaultValue = true },
            new() { Id = "confirmApply", Name = "Confirm Apply", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "outputVariable", Name = "Preview Variable (JSON)", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "folder"));
            var filter = NodeValueHelper.GetString(_props, "filter", "*.*");
            var recursive = NodeValueHelper.GetBool(_props, "recursive", false);
            var mode = NodeValueHelper.GetString(_props, "mode", "prefix").ToLowerInvariant();
            var value = NodeValueHelper.GetString(_props, "value");
            var replacement = NodeValueHelper.GetString(_props, "replacement");
            var template = NodeValueHelper.GetString(_props, "template", "{{name}}{{ext}}");
            var startIndex = NodeValueHelper.GetInt(_props, "startIndex", 1);
            var indexPad = Math.Max(0, NodeValueHelper.GetInt(_props, "indexPad", 3));
            var datePattern = NodeValueHelper.GetString(_props, "datePattern", "yyyyMMdd");
            var dryRun = NodeValueHelper.GetBool(_props, "dryRun", true);
            var confirm = NodeValueHelper.GetBool(_props, "confirmApply", false);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!Directory.Exists(folder))
                return Task.FromResult(NodeResult.Fail($"Folder not found: {folder}"));

            var files = Directory.GetFiles(folder, filter, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).OrderBy(p => p).ToList();
            var preview = new List<(string Source, string Destination)>();
            var errors = new List<string>();
            int idx = startIndex;
            foreach (var path in files)
            {
                try
                {
                    var newName = BuildNewName(path, mode, value, replacement, template, idx, indexPad, datePattern);
                    if (string.Equals(newName, Path.GetFileName(path), StringComparison.Ordinal))
                    {
                        idx++;
                        continue;
                    }
                    var dir = Path.GetDirectoryName(path) ?? folder;
                    preview.Add((path, Path.Combine(dir, newName)));
                }
                catch (Exception ex)
                {
                    errors.Add($"{path}: {ex.Message}");
                }
                idx++;
            }

            var processed = 0;
            var skipped = 0;
            if (!dryRun)
            {
                if (!confirm)
                    return Task.FromResult(NodeResult.Fail("Refusing to rename without confirmApply=true."));

                foreach (var entry in preview)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(entry.Destination))
                        {
                            skipped++;
                            errors.Add($"Destination exists: {entry.Destination}");
                            continue;
                        }
                        File.Move(entry.Source, entry.Destination);
                        processed++;
                    }
                    catch (Exception ex)
                    {
                        skipped++;
                        errors.Add($"{entry.Source}: {ex.Message}");
                    }
                }
            }

            var previewJson = JsonSerializer.Serialize(preview.Select(p => new { source = p.Source, destination = p.Destination }));
            NodeValueHelper.SetVariableIfRequested(context, outVar, previewJson);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["dryRun"] = dryRun,
                ["previewJson"] = previewJson,
                ["plannedCount"] = preview.Count,
                ["renamedCount"] = processed,
                ["skippedCount"] = skipped,
                ["errorsJson"] = JsonSerializer.Serialize(errors)
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Batch rename failed: {ex.Message}"));
        }
    }

    private static string BuildNewName(string path, string mode, string value, string replacement, string template, int index, int indexPad, string datePattern)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var info = new FileInfo(path);
        var indexStr = index.ToString(new string('0', Math.Max(1, indexPad)), CultureInfo.InvariantCulture);
        var dateStr = DateTime.Now.ToString(datePattern, CultureInfo.InvariantCulture);

        switch (mode)
        {
            case "prefix": return $"{value}{name}{ext}";
            case "suffix": return $"{name}{value}{ext}";
            case "replace":
                if (string.IsNullOrEmpty(value)) return name + ext;
                return name.Replace(value, replacement, StringComparison.Ordinal) + ext;
            case "regexreplace":
                if (string.IsNullOrEmpty(value)) return name + ext;
                return Regex.Replace(name, value, replacement) + ext;
            case "sequence":
                return $"{value}{indexStr}{ext}";
            case "datepattern":
                return $"{dateStr}_{name}{ext}";
            case "template":
            default:
                return template
                    .Replace("{{name}}", name)
                    .Replace("{{ext}}", ext.TrimStart('.'))
                    .Replace("{{index}}", indexStr)
                    .Replace("{{date}}", dateStr)
                    .Replace("{{createdAt}}", info.CreationTime.ToString(datePattern, CultureInfo.InvariantCulture))
                    .Replace("{{modifiedAt}}", info.LastWriteTime.ToString(datePattern, CultureInfo.InvariantCulture));
        }
    }
}

[NodeInfo(
    TypeId = "action.createShortcut",
    DisplayName = "Create Shortcut",
    Category = NodeCategory.Action,
    Description = "Create a Windows .lnk shortcut",
    Color = "#22C55E")]
public sealed class CreateShortcutNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.createShortcut",
        DisplayName = "Create Shortcut",
        Category = NodeCategory.Action,
        Description = "Create a .lnk shortcut. Uses PowerShell internally.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "shortcutPath", Name = "Shortcut Path (.lnk)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "targetPath", Name = "Target Path", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "arguments", Name = "Arguments", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "workingDirectory", Name = "Working Directory", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "iconLocation", Name = "Icon Location", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var lnk = context.ResolveTemplate(NodeValueHelper.GetString(_props, "shortcutPath"));
            var target = context.ResolveTemplate(NodeValueHelper.GetString(_props, "targetPath"));
            var args = context.ResolveTemplate(NodeValueHelper.GetString(_props, "arguments"));
            var wd = context.ResolveTemplate(NodeValueHelper.GetString(_props, "workingDirectory"));
            var icon = context.ResolveTemplate(NodeValueHelper.GetString(_props, "iconLocation"));

            if (string.IsNullOrWhiteSpace(lnk) || string.IsNullOrWhiteSpace(target))
                return NodeResult.Fail("Shortcut Path and Target Path are required.");

            if (!lnk.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                lnk += ".lnk";

            var dir = Path.GetDirectoryName(lnk);
            if (!string.IsNullOrEmpty(dir))
                TempFileHelper.EnsureOutputFolder(dir);

            var script = "$ws = New-Object -ComObject WScript.Shell; " +
                         $"$s = $ws.CreateShortcut('{lnk.Replace("'", "''")}'); " +
                         $"$s.TargetPath = '{target.Replace("'", "''")}'; ";
            if (!string.IsNullOrEmpty(args))
                script += $"$s.Arguments = '{args.Replace("'", "''")}'; ";
            if (!string.IsNullOrEmpty(wd))
                script += $"$s.WorkingDirectory = '{wd.Replace("'", "''")}'; ";
            if (!string.IsNullOrEmpty(icon))
                script += $"$s.IconLocation = '{icon.Replace("'", "''")}'; ";
            script += "$s.Save()";

            var outcome = await ProcessRunner.RunAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{script}\"", ct).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
                return NodeResult.Fail($"Shortcut creation failed: {outcome.StdErr}");

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["shortcutPath"] = lnk
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Create shortcut failed: {ex.Message}");
        }
    }
}
