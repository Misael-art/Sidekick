using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

internal static class ImageFormatHelper
{
    public static readonly string[] SupportedFormats = { "png", "jpg", "bmp", "tiff", "gif" };
    public static readonly string[] AllFormats = { "png", "jpg", "bmp", "tiff", "gif", "webp" };

    public static (ImageFormat Format, string Extension)? Resolve(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "png" => (ImageFormat.Png, ".png"),
            "jpg" or "jpeg" => (ImageFormat.Jpeg, ".jpg"),
            "bmp" => (ImageFormat.Bmp, ".bmp"),
            "tiff" or "tif" => (ImageFormat.Tiff, ".tiff"),
            "gif" => (ImageFormat.Gif, ".gif"),
            _ => null
        };
    }

    public static EncoderParameters? BuildJpegQualityParams(int quality)
    {
        var encoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == "image/jpeg");
        if (encoder == null)
            return null;
        var ps = new EncoderParameters(1);
        ps.Param[0] = new EncoderParameter(Encoder.Quality, (long)Math.Clamp(quality, 1, 100));
        return ps;
    }

    public static ImageCodecInfo? GetCodec(string mimeType)
    {
        return ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == mimeType);
    }
}

[NodeInfo(
    TypeId = "action.imageConvert",
    DisplayName = "Image Convert",
    Category = NodeCategory.Action,
    Description = "Convert an image file to png/jpg/bmp/tiff/gif",
    Color = "#22C55E")]
public sealed class ImageConvertNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.imageConvert",
        DisplayName = "Image Convert",
        Category = NodeCategory.Action,
        Description = "Convert an image file. Supports png/jpg/bmp/tiff/gif natively. WebP requires FFmpeg.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}", Description = "Tokens: {{name}}, {{ext}}, {{timestamp}}" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "png", Options = ImageFormatHelper.AllFormats },
            new() { Id = "quality", Name = "Quality (jpg)", Type = PropertyType.Integer, DefaultValue = 90 },
            new() { Id = "overwrite", Name = "Overwrite Existing", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}");
            var format = NodeValueHelper.GetString(_props, "format", "png").ToLowerInvariant();
            var quality = NodeValueHelper.GetInt(_props, "quality", 90);
            var overwrite = NodeValueHelper.GetBool(_props, "overwrite", false);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input))
                return Task.FromResult(NodeResult.Fail($"Input file not found: {input}"));
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            if (format == "webp")
            {
                var ffmpeg = ToolDetector.FindFfmpeg();
                if (ffmpeg == null)
                    return Task.FromResult(NodeResult.Fail("WebP requires FFmpeg. Install from https://ffmpeg.org/ and ensure it is on PATH."));

                var outPath = TempFileHelper.BuildOutputPath(folder, template, input, ".webp");
                if (!overwrite) outPath = TempFileHelper.EnsureUnique(outPath);

                var args = $"-y -i \"{input}\" -quality {Math.Clamp(quality, 1, 100)} \"{outPath}\"";
                var outcome = ProcessRunner.RunAsync(ffmpeg, args, ct).GetAwaiter().GetResult();
                if (outcome.ExitCode != 0)
                    return Task.FromResult(NodeResult.Fail($"FFmpeg WebP encode failed (exit {outcome.ExitCode}): {outcome.StdErr}"));

                using var webImg = Image.FromFile(outPath);
                var w = webImg.Width;
                var h = webImg.Height;
                NodeValueHelper.SetVariableIfRequested(context, outVar, outPath);
                return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
                {
                    ["outputPath"] = outPath,
                    ["width"] = w,
                    ["height"] = h,
                    ["format"] = "webp"
                }));
            }

            var resolved = ImageFormatHelper.Resolve(format);
            if (resolved == null)
                return Task.FromResult(NodeResult.Fail($"Unsupported image format: {format}"));

            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, resolved.Value.Extension);
            if (!overwrite) outputPath = TempFileHelper.EnsureUnique(outputPath);

            using var src = Image.FromFile(input);
            if (resolved.Value.Format.Equals(ImageFormat.Jpeg))
            {
                var codec = ImageFormatHelper.GetCodec("image/jpeg");
                using var jpegParams = ImageFormatHelper.BuildJpegQualityParams(quality);
                if (codec != null && jpegParams != null)
                    src.Save(outputPath, codec, jpegParams);
                else
                    src.Save(outputPath, resolved.Value.Format);
            }
            else
            {
                src.Save(outputPath, resolved.Value.Format);
            }

            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath,
                ["width"] = src.Width,
                ["height"] = src.Height,
                ["format"] = format
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Image convert failed: {ex.Message}"));
        }
    }
}

[NodeInfo(
    TypeId = "action.imageResize",
    DisplayName = "Image Resize",
    Category = NodeCategory.Action,
    Description = "Resize an image with aspect ratio control",
    Color = "#22C55E")]
public sealed class ImageResizeNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.imageResize",
        DisplayName = "Image Resize",
        Category = NodeCategory.Action,
        Description = "Resize an image. Modes: fit (within box, keep ratio), fill (cover, crop), stretch (no ratio).",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}_resized" },
            new() { Id = "width", Name = "Width", Type = PropertyType.Integer, DefaultValue = 800 },
            new() { Id = "height", Name = "Height", Type = PropertyType.Integer, DefaultValue = 600 },
            new() { Id = "keepAspectRatio", Name = "Keep Aspect Ratio", Type = PropertyType.Boolean, DefaultValue = true },
            new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "fit", Options = new[] { "fit", "fill", "stretch" } },
            new() { Id = "format", Name = "Format (optional)", Type = PropertyType.Dropdown, DefaultValue = "", Options = new[] { "", "png", "jpg", "bmp", "tiff", "gif" } },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}_resized");
            var targetW = Math.Max(1, NodeValueHelper.GetInt(_props, "width", 800));
            var targetH = Math.Max(1, NodeValueHelper.GetInt(_props, "height", 600));
            var keepRatio = NodeValueHelper.GetBool(_props, "keepAspectRatio", true);
            var mode = NodeValueHelper.GetString(_props, "mode", "fit").ToLowerInvariant();
            var formatOpt = NodeValueHelper.GetString(_props, "format", "").ToLowerInvariant();
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input))
                return Task.FromResult(NodeResult.Fail($"Input file not found: {input}"));
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            using var src = Image.FromFile(input);

            int newW, newH;
            if (mode == "stretch" || !keepRatio)
            {
                newW = targetW; newH = targetH;
            }
            else
            {
                var rw = (double)targetW / src.Width;
                var rh = (double)targetH / src.Height;
                var ratio = mode == "fill" ? Math.Max(rw, rh) : Math.Min(rw, rh);
                newW = Math.Max(1, (int)Math.Round(src.Width * ratio));
                newH = Math.Max(1, (int)Math.Round(src.Height * ratio));
            }

            using var bitmap = new Bitmap(newW, newH);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(src, 0, 0, newW, newH);
            }

            var ext = !string.IsNullOrEmpty(formatOpt)
                ? "." + formatOpt
                : Path.GetExtension(input);
            var resolved = ImageFormatHelper.Resolve(ext.TrimStart('.')) ?? (ImageFormat.Png, ".png");
            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, resolved.Extension);
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            bitmap.Save(outputPath, resolved.Format);

            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath,
                ["width"] = newW,
                ["height"] = newH
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Image resize failed: {ex.Message}"));
        }
    }
}

[NodeInfo(
    TypeId = "action.imageCompress",
    DisplayName = "Image Compress",
    Category = NodeCategory.Action,
    Description = "Compress a JPG (lossy) with optional max dimensions",
    Color = "#22C55E")]
public sealed class ImageCompressNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.imageCompress",
        DisplayName = "Image Compress",
        Category = NodeCategory.Action,
        Description = "Re-encode JPG with target quality. Optional max width/height (keeps ratio).",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}_compressed" },
            new() { Id = "quality", Name = "Quality (1-100)", Type = PropertyType.Integer, DefaultValue = 75 },
            new() { Id = "maxWidth", Name = "Max Width (optional, 0 = unlimited)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "maxHeight", Name = "Max Height (optional, 0 = unlimited)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}_compressed");
            var quality = Math.Clamp(NodeValueHelper.GetInt(_props, "quality", 75), 1, 100);
            var maxW = NodeValueHelper.GetInt(_props, "maxWidth", 0);
            var maxH = NodeValueHelper.GetInt(_props, "maxHeight", 0);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input))
                return Task.FromResult(NodeResult.Fail($"Input file not found: {input}"));
            if (string.IsNullOrWhiteSpace(folder))
                folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            var originalSize = new FileInfo(input).Length;

            using var src = Image.FromFile(input);
            int newW = src.Width;
            int newH = src.Height;
            if (maxW > 0 && newW > maxW)
            {
                var ratio = (double)maxW / newW;
                newW = maxW;
                newH = Math.Max(1, (int)Math.Round(newH * ratio));
            }
            if (maxH > 0 && newH > maxH)
            {
                var ratio = (double)maxH / newH;
                newH = maxH;
                newW = Math.Max(1, (int)Math.Round(newW * ratio));
            }

            using var bitmap = new Bitmap(newW, newH);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(src, 0, 0, newW, newH);
            }

            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, ".jpg");
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            var codec = ImageFormatHelper.GetCodec("image/jpeg");
            using var ps = ImageFormatHelper.BuildJpegQualityParams(quality);
            if (codec != null && ps != null)
                bitmap.Save(outputPath, codec, ps);
            else
                bitmap.Save(outputPath, ImageFormat.Jpeg);

            var newSize = new FileInfo(outputPath).Length;

            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath,
                ["originalSizeBytes"] = originalSize,
                ["newSizeBytes"] = newSize
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Image compress failed: {ex.Message}"));
        }
    }
}
