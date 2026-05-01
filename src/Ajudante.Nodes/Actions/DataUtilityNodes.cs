using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.clipboardTransform",
    DisplayName = "Clipboard Transform",
    Category = NodeCategory.Action,
    Description = "Transform a string with common operations",
    Color = "#22C55E")]
public sealed class ClipboardTransformNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.clipboardTransform",
        DisplayName = "Clipboard Transform",
        Category = NodeCategory.Action,
        Description = "Transform an input string. Modes: uppercase, lowercase, trim, replace, regexReplace, jsonFormat.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "input", Name = "Input Text (supports {{variables}})", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "uppercase",
                Options = new[] { "uppercase", "lowercase", "trim", "replace", "regexReplace", "jsonFormat" } },
            new() { Id = "find", Name = "Find / Pattern", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "replacement", Name = "Replacement", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "outputVariable", Name = "Output Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "input"));
            var mode = NodeValueHelper.GetString(_props, "mode", "uppercase").ToLowerInvariant();
            var find = NodeValueHelper.GetString(_props, "find");
            var replacement = NodeValueHelper.GetString(_props, "replacement");
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            string result = mode switch
            {
                "uppercase" => input.ToUpperInvariant(),
                "lowercase" => input.ToLowerInvariant(),
                "trim" => input.Trim(),
                "replace" => string.IsNullOrEmpty(find) ? input : input.Replace(find, replacement),
                "regexreplace" => string.IsNullOrEmpty(find) ? input : Regex.Replace(input, find, replacement),
                "jsonformat" => TryFormatJson(input),
                _ => input
            };

            NodeValueHelper.SetVariableIfRequested(context, outVar, result);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["result"] = result,
                ["length"] = result.Length
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Clipboard transform failed: {ex.Message}"));
        }
    }

    private static string TryFormatJson(string input)
    {
        try
        {
            using var doc = JsonDocument.Parse(input);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return input;
        }
    }
}

[NodeInfo(
    TypeId = "action.extractMetadata",
    DisplayName = "Extract Metadata",
    Category = NodeCategory.Action,
    Description = "Extract metadata from image/media/pdf files",
    Color = "#22C55E")]
public sealed class ExtractMetadataNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.extractMetadata",
        DisplayName = "Extract Metadata",
        Category = NodeCategory.Action,
        Description = "Inspect a file. Images natively. Media via FFprobe (if installed). PDF via PdfSharp.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "kind", Name = "Kind", Type = PropertyType.Dropdown, DefaultValue = "auto",
                Options = new[] { "auto", "image", "media", "pdf" } },
            new() { Id = "outputVariable", Name = "Metadata Variable (JSON)", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var kindRaw = NodeValueHelper.GetString(_props, "kind", "auto").ToLowerInvariant();
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input))
                return NodeResult.Fail($"Input file not found: {input}");

            var ext = Path.GetExtension(input).ToLowerInvariant();
            var kind = kindRaw == "auto" ? GuessKind(ext) : kindRaw;

            Dictionary<string, object?> metadata = kind switch
            {
                "image" => ExtractImageMeta(input),
                "media" => await ExtractMediaMetaAsync(input, ct).ConfigureAwait(false),
                "pdf" => ExtractPdfMeta(input),
                _ => new Dictionary<string, object?> { ["error"] = $"Unsupported kind for {ext}" }
            };

            var json = JsonSerializer.Serialize(metadata);
            NodeValueHelper.SetVariableIfRequested(context, outVar, json);

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["metadata"] = metadata,
                ["metadataJson"] = json
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Extract metadata failed: {ex.Message}");
        }
    }

    private static string GuessKind(string ext) => ext switch
    {
        ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tiff" or ".webp" => "image",
        ".mp4" or ".mov" or ".mkv" or ".avi" or ".webm" or ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "media",
        ".pdf" => "pdf",
        _ => "unknown"
    };

    private static Dictionary<string, object?> ExtractImageMeta(string path)
    {
        using var img = Image.FromFile(path);
        var info = new FileInfo(path);
        var dict = new Dictionary<string, object?>
        {
            ["width"] = img.Width,
            ["height"] = img.Height,
            ["pixelFormat"] = img.PixelFormat.ToString(),
            ["horizontalResolution"] = img.HorizontalResolution,
            ["verticalResolution"] = img.VerticalResolution,
            ["sizeBytes"] = info.Length,
            ["createdAt"] = info.CreationTime.ToString("o"),
            ["modifiedAt"] = info.LastWriteTime.ToString("o")
        };
        return dict;
    }

    private static async Task<Dictionary<string, object?>> ExtractMediaMetaAsync(string path, CancellationToken ct)
    {
        var ffprobe = ToolDetector.FindFfprobe();
        if (ffprobe == null)
        {
            return new Dictionary<string, object?>
            {
                ["error"] = "FFprobe not found. Install FFmpeg from https://ffmpeg.org/ to enable media metadata.",
                ["sizeBytes"] = new FileInfo(path).Length
            };
        }
        var args = $"-v quiet -print_format json -show_format -show_streams \"{path}\"";
        var outcome = await ProcessRunner.RunAsync(ffprobe, args, ct).ConfigureAwait(false);
        if (outcome.ExitCode != 0)
        {
            return new Dictionary<string, object?>
            {
                ["error"] = $"FFprobe failed: {outcome.StdErr}",
                ["sizeBytes"] = new FileInfo(path).Length
            };
        }
        try
        {
            using var doc = JsonDocument.Parse(outcome.StdOut);
            return new Dictionary<string, object?> { ["ffprobe"] = JsonSerializer.Deserialize<object>(doc.RootElement.GetRawText()) };
        }
        catch
        {
            return new Dictionary<string, object?> { ["raw"] = outcome.StdOut };
        }
    }

    private static Dictionary<string, object?> ExtractPdfMeta(string path)
    {
        try
        {
            using var doc = PdfSharpCore.Pdf.IO.PdfReader.Open(path, PdfSharpCore.Pdf.IO.PdfDocumentOpenMode.InformationOnly);
            return new Dictionary<string, object?>
            {
                ["pageCount"] = doc.PageCount,
                ["title"] = doc.Info.Title,
                ["author"] = doc.Info.Author,
                ["subject"] = doc.Info.Subject,
                ["keywords"] = doc.Info.Keywords,
                ["creator"] = doc.Info.Creator,
                ["sizeBytes"] = new FileInfo(path).Length
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object?> { ["error"] = ex.Message };
        }
    }
}
