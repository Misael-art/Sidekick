using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;

namespace Ajudante.Nodes.Actions;

internal static class FfmpegHelpers
{
    public const string MissingMessage =
        "FFmpeg not found. Install from https://ffmpeg.org/ and ensure ffmpeg.exe is on PATH or in C:\\Program Files\\ffmpeg\\bin\\.";
}

[NodeInfo(
    TypeId = "action.videoConvert",
    DisplayName = "Video Convert",
    Category = NodeCategory.Action,
    Description = "Convert a video to another container/codec (requires FFmpeg)",
    Color = "#22C55E")]
public sealed class VideoConvertNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.videoConvert",
        DisplayName = "Video Convert",
        Category = NodeCategory.Action,
        Description = "Convert video file. Requires FFmpeg.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "mp4", Options = new[] { "mp4", "webm", "avi", "mov", "mkv" } },
            new() { Id = "codec", Name = "Video Codec (optional)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "resolution", Name = "Resolution (eg 1280x720, optional)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "fps", Name = "FPS (0 = source)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var ffmpeg = ToolDetector.FindFfmpeg();
            if (ffmpeg == null) return NodeResult.Fail(FfmpegHelpers.MissingMessage);

            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}");
            var format = NodeValueHelper.GetString(_props, "format", "mp4").ToLowerInvariant();
            var codec = NodeValueHelper.GetString(_props, "codec");
            var resolution = NodeValueHelper.GetString(_props, "resolution");
            var fps = NodeValueHelper.GetInt(_props, "fps", 0);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input)) return NodeResult.Fail($"Input file not found: {input}");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, "." + format);
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            var args = $"-y -i \"{input}\"";
            if (!string.IsNullOrEmpty(codec)) args += $" -c:v {codec}";
            if (!string.IsNullOrEmpty(resolution)) args += $" -s {resolution}";
            if (fps > 0) args += $" -r {fps}";
            args += $" \"{outputPath}\"";

            var outcome = await ProcessRunner.RunAsync(ffmpeg, args, ct).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
                return NodeResult.Fail($"FFmpeg failed (exit {outcome.ExitCode}): {outcome.StdErr}");

            var size = new FileInfo(outputPath).Length;
            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath,
                ["sizeBytes"] = size
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Video convert failed: {ex.Message}");
        }
    }
}

[NodeInfo(
    TypeId = "action.videoExtractAudio",
    DisplayName = "Video Extract Audio",
    Category = NodeCategory.Action,
    Description = "Extract the audio track from a video (requires FFmpeg)",
    Color = "#22C55E")]
public sealed class VideoExtractAudioNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.videoExtractAudio",
        DisplayName = "Video Extract Audio",
        Category = NodeCategory.Action,
        Description = "Extract audio from a video file. Requires FFmpeg.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}" },
            new() { Id = "audioFormat", Name = "Audio Format", Type = PropertyType.Dropdown, DefaultValue = "mp3", Options = new[] { "mp3", "wav", "aac", "flac" } },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var ffmpeg = ToolDetector.FindFfmpeg();
            if (ffmpeg == null) return NodeResult.Fail(FfmpegHelpers.MissingMessage);

            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}");
            var fmt = NodeValueHelper.GetString(_props, "audioFormat", "mp3").ToLowerInvariant();
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input)) return NodeResult.Fail($"Input file not found: {input}");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, "." + fmt);
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            var args = $"-y -i \"{input}\" -vn \"{outputPath}\"";
            var outcome = await ProcessRunner.RunAsync(ffmpeg, args, ct).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
                return NodeResult.Fail($"FFmpeg failed (exit {outcome.ExitCode}): {outcome.StdErr}");

            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Extract audio failed: {ex.Message}");
        }
    }
}

[NodeInfo(
    TypeId = "action.audioConvert",
    DisplayName = "Audio Convert",
    Category = NodeCategory.Action,
    Description = "Convert an audio file (requires FFmpeg)",
    Color = "#22C55E")]
public sealed class AudioConvertNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.audioConvert",
        DisplayName = "Audio Convert",
        Category = NodeCategory.Action,
        Description = "Convert audio with optional bitrate / sample rate. Requires FFmpeg.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "mp3", Options = new[] { "mp3", "wav", "flac", "aac", "ogg" } },
            new() { Id = "bitrate", Name = "Bitrate (eg 192k, optional)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "sampleRate", Name = "Sample Rate (eg 44100, 0 = source)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var ffmpeg = ToolDetector.FindFfmpeg();
            if (ffmpeg == null) return NodeResult.Fail(FfmpegHelpers.MissingMessage);

            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}");
            var format = NodeValueHelper.GetString(_props, "format", "mp3").ToLowerInvariant();
            var bitrate = NodeValueHelper.GetString(_props, "bitrate");
            var sampleRate = NodeValueHelper.GetInt(_props, "sampleRate", 0);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input)) return NodeResult.Fail($"Input file not found: {input}");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, "." + format);
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            var args = $"-y -i \"{input}\"";
            if (!string.IsNullOrEmpty(bitrate)) args += $" -b:a {bitrate}";
            if (sampleRate > 0) args += $" -ar {sampleRate}";
            args += $" \"{outputPath}\"";

            var outcome = await ProcessRunner.RunAsync(ffmpeg, args, ct).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
                return NodeResult.Fail($"FFmpeg failed (exit {outcome.ExitCode}): {outcome.StdErr}");

            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Audio convert failed: {ex.Message}");
        }
    }
}

[NodeInfo(
    TypeId = "action.audioNormalize",
    DisplayName = "Audio Normalize",
    Category = NodeCategory.Action,
    Description = "Normalize audio loudness via EBU R128 (requires FFmpeg)",
    Color = "#22C55E")]
public sealed class AudioNormalizeNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.audioNormalize",
        DisplayName = "Audio Normalize",
        Category = NodeCategory.Action,
        Description = "Apply FFmpeg loudnorm to reach target LUFS. Requires FFmpeg.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputNameTemplate", Name = "Output Name Template", Type = PropertyType.String, DefaultValue = "{{name}}_normalized" },
            new() { Id = "targetLevel", Name = "Target LUFS (default -16)", Type = PropertyType.Float, DefaultValue = -16.0 },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var ffmpeg = ToolDetector.FindFfmpeg();
            if (ffmpeg == null) return NodeResult.Fail(FfmpegHelpers.MissingMessage);

            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var template = NodeValueHelper.GetString(_props, "outputNameTemplate", "{{name}}_normalized");
            var target = NodeValueHelper.GetDouble(_props, "targetLevel", -16.0);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input)) return NodeResult.Fail($"Input file not found: {input}");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();

            var ext = Path.GetExtension(input);
            if (string.IsNullOrEmpty(ext)) ext = ".mp3";
            var outputPath = TempFileHelper.BuildOutputPath(folder, template, input, ext);
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            var args = $"-y -i \"{input}\" -af loudnorm=I={target.ToString(System.Globalization.CultureInfo.InvariantCulture)}:TP=-1.5:LRA=11 \"{outputPath}\"";
            var outcome = await ProcessRunner.RunAsync(ffmpeg, args, ct).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
                return NodeResult.Fail($"FFmpeg loudnorm failed (exit {outcome.ExitCode}): {outcome.StdErr}");

            NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = outputPath
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Audio normalize failed: {ex.Message}");
        }
    }
}
