using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Automation;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using Ajudante.Platform.Screen;
using Ajudante.Platform.UIAutomation;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.captureScreenshot",
    DisplayName = "Capture Screenshot",
    Category = NodeCategory.Action,
    Description = "Captures a desktop screenshot from monitor, region, active window, or selector target",
    Color = "#22C55E")]
public sealed class CaptureScreenshotNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.captureScreenshot",
        DisplayName = "Capture Screenshot",
        Category = NodeCategory.Action,
        Description = "Captures a desktop screenshot from monitor, region, active window, or selector target",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Captured", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String },
            new() { Id = "width", Name = "Width", DataType = PortDataType.Number },
            new() { Id = "height", Name = "Height", DataType = PortDataType.Number },
            new() { Id = "target", Name = "Target", DataType = PortDataType.String },
            new() { Id = "errorMessage", Name = "Error Message", DataType = PortDataType.String }
        },
        Properties = BuildCaptureProperties()
    };

    public void Configure(Dictionary<string, object?> properties) =>
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var settings = CaptureSettings.From(context, _properties);
            var format = ParseImageFormat(settings.Format, settings.Quality);
            ct.ThrowIfCancellationRequested();

            if (settings.DelayMs > 0)
                Task.Delay(settings.DelayMs, ct).GetAwaiter().GetResult();

            using var bitmap = CaptureBitmap(settings);
            using var transformed = ApplyCaptureTransforms(bitmap, settings);
            var outputPath = ResolveOutputFilePath(context, settings.OutputPath, settings.OutputFolder, settings.FileNameTemplate, settings.Format);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            SaveBitmap(transformed, outputPath, format, settings.Quality);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["filePath"] = outputPath,
                ["width"] = transformed.Width,
                ["height"] = transformed.Height,
                ["target"] = settings.Target,
                ["error"] = ""
            }));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["error"] = "Capture cancelled"
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Screenshot capture failed: {ex.Message}"));
        }
    }

    private static List<PropertyDefinition> BuildCaptureProperties()
    {
        return
        [
            new() { Id = "target", Name = "Target", Type = PropertyType.Dropdown, DefaultValue = "fullDesktop", Options = ["fullDesktop", "monitor", "region", "activeWindow", "windowSelector"] },
            new() { Id = "outputPath", Name = "Output Path", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filenameTemplate", Name = "Filename Template", Type = PropertyType.String, DefaultValue = "screenshot_{{timestamp}}" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "png", Options = ["png", "jpg", "bmp"] },
            new() { Id = "quality", Name = "JPG Quality", Type = PropertyType.Integer, DefaultValue = 90 },
            new() { Id = "delayMs", Name = "Delay (ms)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "includeCursor", Name = "Include Cursor", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "monitorIndex", Name = "Monitor Index", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "x", Name = "X", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "y", Name = "Y", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "width", Name = "Width", Type = PropertyType.Integer, DefaultValue = 1280 },
            new() { Id = "height", Name = "Height", Type = PropertyType.Integer, DefaultValue = 720 },
            new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "windowTitleMatch", Name = "Window Title Match", Type = PropertyType.Dropdown, DefaultValue = "contains", Options = ["equals", "contains", "regex"] },
            new() { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "scaleWidth", Name = "Scale Width", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "scaleHeight", Name = "Scale Height", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "effect", Name = "Effect", Type = PropertyType.Dropdown, DefaultValue = "none", Options = ["none", "grayscale", "blur", "highlightCursor"] }
        ];
    }

    private static Bitmap CaptureBitmap(CaptureSettings settings)
    {
        return settings.Target switch
        {
            "monitor" => CaptureMonitor(settings.MonitorIndex),
            "region" => ScreenCapture.CaptureRegion(settings.X, settings.Y, settings.Width, settings.Height),
            "activeWindow" => ScreenCapture.CaptureWindow(GetActiveWindowHandle()),
            "windowSelector" => ScreenCapture.CaptureWindow(FindWindowHandle(settings)),
            _ => ScreenCapture.CaptureScreen()
        };
    }

    private static Bitmap CaptureMonitor(int monitorIndex)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return ScreenCapture.CaptureScreen();

        var safeIndex = Math.Clamp(monitorIndex, 0, screens.Length - 1);
        var bounds = screens[safeIndex].Bounds;
        return ScreenCapture.CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static Bitmap ApplyCaptureTransforms(Bitmap source, CaptureSettings settings)
    {
        var working = new Bitmap(source);

        if (settings.IncludeCursor || string.Equals(settings.Effect, "highlightCursor", StringComparison.OrdinalIgnoreCase))
            DrawCursorIndicator(working, sourceBounds: new Rectangle(0, 0, working.Width, working.Height), includeIcon: settings.IncludeCursor);

        if (string.Equals(settings.Effect, "grayscale", StringComparison.OrdinalIgnoreCase))
            ApplyGrayscale(working);
        else if (string.Equals(settings.Effect, "blur", StringComparison.OrdinalIgnoreCase))
            ApplySimpleBlur(working);

        if (settings.ScaleWidth > 0 && settings.ScaleHeight > 0)
        {
            var scaled = new Bitmap(settings.ScaleWidth, settings.ScaleHeight);
            using var graphics = Graphics.FromImage(scaled);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(working, 0, 0, scaled.Width, scaled.Height);
            working.Dispose();
            return scaled;
        }

        return working;
    }

    private static void ApplyGrayscale(Bitmap bitmap)
    {
        using var graphics = Graphics.FromImage(bitmap);
        var colorMatrix = new ColorMatrix(new[]
        {
            new[] { 0.3f, 0.3f, 0.3f, 0f, 0f },
            new[] { 0.59f, 0.59f, 0.59f, 0f, 0f },
            new[] { 0.11f, 0.11f, 0.11f, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f }
        });
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(colorMatrix);
        graphics.DrawImage(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
    }

    private static void ApplySimpleBlur(Bitmap bitmap)
    {
        var reducedWidth = Math.Max(1, bitmap.Width / 6);
        var reducedHeight = Math.Max(1, bitmap.Height / 6);

        using var reduced = new Bitmap(reducedWidth, reducedHeight);
        using (var graphics = Graphics.FromImage(reduced))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(bitmap, 0, 0, reducedWidth, reducedHeight);
        }

        using var graphicsOut = Graphics.FromImage(bitmap);
        graphicsOut.InterpolationMode = InterpolationMode.HighQualityBilinear;
        graphicsOut.DrawImage(reduced, 0, 0, bitmap.Width, bitmap.Height);
    }

    private static void DrawCursorIndicator(Bitmap bitmap, Rectangle sourceBounds, bool includeIcon)
    {
        var cursorPoint = Cursor.Position;
        var localX = cursorPoint.X - sourceBounds.X;
        var localY = cursorPoint.Y - sourceBounds.Y;
        if (localX < 0 || localY < 0 || localX >= bitmap.Width || localY >= bitmap.Height)
            return;

        using var graphics = Graphics.FromImage(bitmap);
        using var outerPen = new Pen(Color.OrangeRed, 3f);
        using var innerPen = new Pen(Color.White, 1.5f);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.DrawEllipse(outerPen, localX - 14, localY - 14, 28, 28);
        graphics.DrawEllipse(innerPen, localX - 8, localY - 8, 16, 16);

        if (includeIcon)
        {
            try { Cursors.Default.Draw(graphics, new Rectangle(localX, localY, 20, 20)); }
            catch { /* best effort */ }
        }
    }

    private static void SaveBitmap(Bitmap bitmap, string outputPath, ImageFormatInfo format, int quality)
    {
        if (format.Format == ImageFormat.Jpeg && format.Encoder is not null)
        {
            var clamped = Math.Clamp(quality, 1, 100);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, clamped);
            bitmap.Save(outputPath, format.Encoder, encoderParams);
            return;
        }

        bitmap.Save(outputPath, format.Format);
    }

    private static IntPtr FindWindowHandle(CaptureSettings settings)
    {
        var window = AutomationElementLocator.FindElement(
            settings.WindowTitle,
            automationId: "",
            name: "",
            controlType: "window",
            timeoutMs: Math.Max(500, settings.DelayMs),
            processName: settings.ProcessName,
            processPath: settings.ProcessPath,
            titleMatch: AutomationElementLocator.ParseTitleMatch(settings.WindowTitleMatch));

        if (window is null)
            throw new InvalidOperationException("Window selector target not found for screenshot capture.");

        var hwnd = window.Current.NativeWindowHandle;
        if (hwnd == 0)
            throw new InvalidOperationException("Selected window does not expose a valid window handle.");

        return new IntPtr(hwnd);
    }

    private static string ResolveOutputFilePath(FlowExecutionContext context, string outputPath, string outputFolder, string fileNameTemplate, string format)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return context.ResolveTemplate(outputPath);

        var rootFolder = string.IsNullOrWhiteSpace(outputFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Sidekick")
            : context.ResolveTemplate(outputFolder);

        var resolvedTemplate = context.ResolveTemplate(string.IsNullOrWhiteSpace(fileNameTemplate) ? "capture_{{timestamp}}" : fileNameTemplate);
        var safeTemplate = resolvedTemplate.Replace("{{timestamp}}", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        foreach (var invalid in Path.GetInvalidFileNameChars())
            safeTemplate = safeTemplate.Replace(invalid, '_');

        return Path.Combine(rootFolder, $"{safeTemplate}.{NormalizeFormat(format)}");
    }

    private static ImageFormatInfo ParseImageFormat(string format, int quality)
    {
        _ = quality;
        return NormalizeFormat(format) switch
        {
            "png" => new ImageFormatInfo(ImageFormat.Png, null),
            "jpg" => new ImageFormatInfo(ImageFormat.Jpeg, ImageCodecInfo.GetImageEncoders().FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid)),
            "bmp" => new ImageFormatInfo(ImageFormat.Bmp, null),
            _ => throw new InvalidOperationException($"Unsupported screenshot format '{format}'. Supported formats: png, jpg, bmp.")
        };
    }

    private static string NormalizeFormat(string format)
    {
        var normalized = (format ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "jpeg" => "jpg",
            "" => "png",
            _ => normalized
        };
    }

    private static IntPtr GetActiveWindowHandle()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Active window handle not found.");

        return hwnd;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private sealed record ImageFormatInfo(ImageFormat Format, ImageCodecInfo? Encoder);

    private sealed record CaptureSettings(
        string Target,
        string OutputPath,
        string OutputFolder,
        string FileNameTemplate,
        string Format,
        int Quality,
        int DelayMs,
        bool IncludeCursor,
        int MonitorIndex,
        int X,
        int Y,
        int Width,
        int Height,
        string WindowTitle,
        string WindowTitleMatch,
        string ProcessName,
        string ProcessPath,
        int ScaleWidth,
        int ScaleHeight,
        string Effect)
    {
        public static CaptureSettings From(FlowExecutionContext context, Dictionary<string, object?> properties)
        {
            return new CaptureSettings(
                Target: context.ResolveTemplate(NodeValueHelper.GetString(properties, "target", "fullDesktop")),
                OutputPath: NodeValueHelper.ResolveTemplateProperty(context, properties, "outputPath"),
                OutputFolder: NodeValueHelper.ResolveTemplateProperty(context, properties, "outputFolder"),
                FileNameTemplate: NodeValueHelper.ResolveTemplateProperty(context, properties, "filenameTemplate", "capture_{{timestamp}}"),
                Format: NodeValueHelper.ResolveTemplateProperty(context, properties, "format", "png"),
                Quality: NodeValueHelper.GetInt(properties, "quality", 90),
                DelayMs: NodeValueHelper.GetInt(properties, "delayMs", 0),
                IncludeCursor: NodeValueHelper.GetBool(properties, "includeCursor", false),
                MonitorIndex: NodeValueHelper.GetInt(properties, "monitorIndex", 0),
                X: NodeValueHelper.GetInt(properties, "x", 0),
                Y: NodeValueHelper.GetInt(properties, "y", 0),
                Width: Math.Max(1, NodeValueHelper.GetInt(properties, "width", 1280)),
                Height: Math.Max(1, NodeValueHelper.GetInt(properties, "height", 720)),
                WindowTitle: NodeValueHelper.ResolveTemplateProperty(context, properties, "windowTitle"),
                WindowTitleMatch: NodeValueHelper.ResolveTemplateProperty(context, properties, "windowTitleMatch", "contains"),
                ProcessName: NodeValueHelper.ResolveTemplateProperty(context, properties, "processName"),
                ProcessPath: NodeValueHelper.ResolveTemplateProperty(context, properties, "processPath"),
                ScaleWidth: NodeValueHelper.GetInt(properties, "scaleWidth", 0),
                ScaleHeight: NodeValueHelper.GetInt(properties, "scaleHeight", 0),
                Effect: NodeValueHelper.ResolveTemplateProperty(context, properties, "effect", "none"));
        }
    }
}

[NodeInfo(
    TypeId = "action.recordDesktop",
    DisplayName = "Record Desktop",
    Category = NodeCategory.Action,
    Description = "Records desktop frames to a local video file using ScreenCapture + Emgu CV VideoWriter",
    Color = "#22C55E")]
public sealed class RecordDesktopNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.recordDesktop",
        DisplayName = "Record Desktop",
        Category = NodeCategory.Action,
        Description = "Records desktop frames to a local video file using ScreenCapture + Emgu CV VideoWriter",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Recorded", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String },
            new() { Id = "durationMs", Name = "Duration (ms)", DataType = PortDataType.Number },
            new() { Id = "framesWritten", Name = "Frames Written", DataType = PortDataType.Number },
            new() { Id = "fps", Name = "FPS", DataType = PortDataType.Number },
            new() { Id = "target", Name = "Target", DataType = PortDataType.String },
            new() { Id = "errorMessage", Name = "Error Message", DataType = PortDataType.String }
        },
        Properties = BuildDesktopRecordProperties()
    };

    public void Configure(Dictionary<string, object?> properties) =>
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var settings = RecordDesktopSettings.From(context, _properties);
            ValidateRecordDesktopSettings(settings);
            if (settings.CountdownMs > 0)
                await Task.Delay(settings.CountdownMs, ct);

            var outputPath = ResolveVideoOutputPath(context, settings.OutputPath, settings.OutputFolder, settings.FileNameTemplate, settings.Format);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            var stopwatch = Stopwatch.StartNew();
            var framesWritten = 0;
            var frameIntervalMs = 1000.0 / settings.Fps;

            using var firstFrame = CaptureDesktopTarget(settings);
            using var writer = CreateWriter(outputPath, settings.Codec, settings.Fps, firstFrame.Size);

            await WriteFrameAsync(writer, firstFrame, ct);
            framesWritten++;

            while (stopwatch.ElapsedMilliseconds < settings.DurationMs)
            {
                ct.ThrowIfCancellationRequested();

                using var frame = CaptureDesktopTarget(settings);
                await WriteFrameAsync(writer, frame, ct);
                framesWritten++;

                if (settings.MaxFileSizeMb > 0)
                {
                    var maxBytes = settings.MaxFileSizeMb * 1024L * 1024L;
                    if (new FileInfo(outputPath).Length >= maxBytes)
                        break;
                }

                var expectedElapsed = framesWritten * frameIntervalMs;
                var sleepMs = (int)Math.Max(0, expectedElapsed - stopwatch.ElapsedMilliseconds);
                if (sleepMs > 0)
                    await Task.Delay(sleepMs, ct);
            }

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["filePath"] = outputPath,
                ["durationMs"] = stopwatch.ElapsedMilliseconds,
                ["framesWritten"] = framesWritten,
                ["fps"] = settings.Fps,
                ["target"] = settings.Target,
                ["error"] = ""
            });
        }
        catch (OperationCanceledException)
        {
            return NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["error"] = "Desktop recording cancelled"
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Desktop recording failed: {ex.Message}");
        }
    }

    private static List<PropertyDefinition> BuildDesktopRecordProperties()
    {
        return
        [
            new() { Id = "target", Name = "Target", Type = PropertyType.Dropdown, DefaultValue = "fullDesktop", Options = ["fullDesktop", "monitor", "region", "windowSelector"] },
            new() { Id = "durationMs", Name = "Duration (ms)", Type = PropertyType.Integer, DefaultValue = 5000 },
            new() { Id = "fps", Name = "FPS", Type = PropertyType.Integer, DefaultValue = 12 },
            new() { Id = "outputPath", Name = "Output Path", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filenameTemplate", Name = "Filename Template", Type = PropertyType.String, DefaultValue = "desktop_record_{{timestamp}}" },
            new() { Id = "codec", Name = "Codec", Type = PropertyType.String, DefaultValue = "MJPG" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "avi", Options = ["avi", "mp4"] },
            new() { Id = "monitorIndex", Name = "Monitor Index", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "x", Name = "X", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "y", Name = "Y", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "width", Name = "Width", Type = PropertyType.Integer, DefaultValue = 1280 },
            new() { Id = "height", Name = "Height", Type = PropertyType.Integer, DefaultValue = 720 },
            new() { Id = "includeCursor", Name = "Include Cursor", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "countdownMs", Name = "Countdown (ms)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "showRecordingIndicator", Name = "Show Recording Indicator", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "maxFileSizeMb", Name = "Max File Size (MB)", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "windowTitle", Name = "Window Title", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "windowTitleMatch", Name = "Window Title Match", Type = PropertyType.Dropdown, DefaultValue = "contains", Options = ["equals", "contains", "regex"] },
            new() { Id = "processName", Name = "Process Name", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "processPath", Name = "Process Path", Type = PropertyType.FilePath, DefaultValue = "" }
        ];
    }

    private static void ValidateRecordDesktopSettings(RecordDesktopSettings settings)
    {
        if (settings.DurationMs <= 0)
            throw new InvalidOperationException("Desktop recording durationMs must be greater than zero.");
        if (settings.Fps <= 0)
            throw new InvalidOperationException("Desktop recording fps must be greater than zero.");
    }

    private static Bitmap CaptureDesktopTarget(RecordDesktopSettings settings)
    {
        return settings.Target switch
        {
            "monitor" => CaptureMonitor(settings.MonitorIndex),
            "region" => ScreenCapture.CaptureRegion(settings.X, settings.Y, settings.Width, settings.Height),
            "windowSelector" => ScreenCapture.CaptureWindow(FindWindowHandle(settings.WindowTitle, settings.WindowTitleMatch, settings.ProcessName, settings.ProcessPath)),
            _ => ScreenCapture.CaptureScreen()
        };
    }

    private static Bitmap CaptureMonitor(int monitorIndex)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
            return ScreenCapture.CaptureScreen();

        var safeIndex = Math.Clamp(monitorIndex, 0, screens.Length - 1);
        var bounds = screens[safeIndex].Bounds;
        return ScreenCapture.CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private static IntPtr FindWindowHandle(string windowTitle, string windowTitleMatch, string processName, string processPath)
    {
        var window = AutomationElementLocator.FindElement(
            windowTitle,
            automationId: "",
            name: "",
            controlType: "window",
            timeoutMs: 1500,
            processName: processName,
            processPath: processPath,
            titleMatch: AutomationElementLocator.ParseTitleMatch(windowTitleMatch));

        if (window is null)
            throw new InvalidOperationException("Window selector target not found for desktop recording.");

        var hwnd = window.Current.NativeWindowHandle;
        if (hwnd == 0)
            throw new InvalidOperationException("Selected desktop window has no valid NativeWindowHandle.");

        return new IntPtr(hwnd);
    }

    private static async Task WriteFrameAsync(VideoWriter writer, Bitmap frame, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var mat = ConvertBitmapToBgrMat(frame);
        writer.Write(mat);
        await Task.CompletedTask;
    }

    private static Mat ConvertBitmapToBgrMat(Bitmap frame)
    {
        var rect = new Rectangle(0, 0, frame.Width, frame.Height);
        var data = frame.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            using var argb = new Mat(frame.Height, frame.Width, DepthType.Cv8U, 4, data.Scan0, data.Stride);
            var bgr = new Mat();
            CvInvoke.CvtColor(argb, bgr, ColorConversion.Bgra2Bgr);
            return bgr;
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    private static VideoWriter CreateWriter(string outputPath, string codec, int fps, Size frameSize)
    {
        var safeCodec = NormalizeCodec(codec);
        var writer = new VideoWriter(outputPath, VideoWriter.Fourcc(safeCodec[0], safeCodec[1], safeCodec[2], safeCodec[3]), fps, frameSize, true);
        if (!writer.IsOpened)
            throw new InvalidOperationException("Unable to open VideoWriter. Check codec/format compatibility on this machine.");

        return writer;
    }

    private static string NormalizeCodec(string codec)
    {
        var normalized = (codec ?? "").Trim().ToUpperInvariant();
        if (normalized.Length != 4)
            return "MJPG";

        return normalized;
    }

    private static string ResolveVideoOutputPath(FlowExecutionContext context, string outputPath, string outputFolder, string fileNameTemplate, string format)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return context.ResolveTemplate(outputPath);

        var rootFolder = string.IsNullOrWhiteSpace(outputFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Sidekick")
            : context.ResolveTemplate(outputFolder);

        var template = context.ResolveTemplate(string.IsNullOrWhiteSpace(fileNameTemplate) ? "record_{{timestamp}}" : fileNameTemplate);
        var safe = template.Replace("{{timestamp}}", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        foreach (var invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');

        return Path.Combine(rootFolder, $"{safe}.{NormalizeVideoFormat(format)}");
    }

    private static string NormalizeVideoFormat(string format)
    {
        var normalized = (format ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            "mp4" => "mp4",
            _ => "avi"
        };
    }

    private sealed record RecordDesktopSettings(
        string Target,
        int DurationMs,
        int Fps,
        string OutputPath,
        string OutputFolder,
        string FileNameTemplate,
        string Codec,
        string Format,
        int MonitorIndex,
        int X,
        int Y,
        int Width,
        int Height,
        bool IncludeCursor,
        int CountdownMs,
        bool ShowRecordingIndicator,
        int MaxFileSizeMb,
        string WindowTitle,
        string WindowTitleMatch,
        string ProcessName,
        string ProcessPath)
    {
        public static RecordDesktopSettings From(FlowExecutionContext context, Dictionary<string, object?> properties)
        {
            return new RecordDesktopSettings(
                Target: NodeValueHelper.ResolveTemplateProperty(context, properties, "target", "fullDesktop"),
                DurationMs: NodeValueHelper.GetInt(properties, "durationMs", 5000),
                Fps: NodeValueHelper.GetInt(properties, "fps", 12),
                OutputPath: NodeValueHelper.ResolveTemplateProperty(context, properties, "outputPath"),
                OutputFolder: NodeValueHelper.ResolveTemplateProperty(context, properties, "outputFolder"),
                FileNameTemplate: NodeValueHelper.ResolveTemplateProperty(context, properties, "filenameTemplate", "desktop_record_{{timestamp}}"),
                Codec: NodeValueHelper.ResolveTemplateProperty(context, properties, "codec", "MJPG"),
                Format: NodeValueHelper.ResolveTemplateProperty(context, properties, "format", "avi"),
                MonitorIndex: NodeValueHelper.GetInt(properties, "monitorIndex", 0),
                X: NodeValueHelper.GetInt(properties, "x", 0),
                Y: NodeValueHelper.GetInt(properties, "y", 0),
                Width: Math.Max(1, NodeValueHelper.GetInt(properties, "width", 1280)),
                Height: Math.Max(1, NodeValueHelper.GetInt(properties, "height", 720)),
                IncludeCursor: NodeValueHelper.GetBool(properties, "includeCursor", false),
                CountdownMs: NodeValueHelper.GetInt(properties, "countdownMs", 0),
                ShowRecordingIndicator: NodeValueHelper.GetBool(properties, "showRecordingIndicator", false),
                MaxFileSizeMb: NodeValueHelper.GetInt(properties, "maxFileSizeMb", 0),
                WindowTitle: NodeValueHelper.ResolveTemplateProperty(context, properties, "windowTitle"),
                WindowTitleMatch: NodeValueHelper.ResolveTemplateProperty(context, properties, "windowTitleMatch", "contains"),
                ProcessName: NodeValueHelper.ResolveTemplateProperty(context, properties, "processName"),
                ProcessPath: NodeValueHelper.ResolveTemplateProperty(context, properties, "processPath"));
        }
    }
}

[NodeInfo(
    TypeId = "action.recordCamera",
    DisplayName = "Record Camera",
    Category = NodeCategory.Action,
    Description = "Records camera video using Emgu CV VideoCapture + VideoWriter",
    Color = "#22C55E")]
public sealed class RecordCameraNode : IActionNode
{
    private Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.recordCamera",
        DisplayName = "Record Camera",
        Category = NodeCategory.Action,
        Description = "Records camera video using Emgu CV VideoCapture + VideoWriter",
        Color = "#22C55E",
        InputPorts = new List<PortDefinition> { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new List<PortDefinition>
        {
            new() { Id = "out", Name = "Recorded", DataType = PortDataType.Flow },
            new() { Id = "error", Name = "Error", DataType = PortDataType.Flow },
            new() { Id = "filePath", Name = "File Path", DataType = PortDataType.String },
            new() { Id = "cameraName", Name = "Camera Name", DataType = PortDataType.String },
            new() { Id = "width", Name = "Width", DataType = PortDataType.Number },
            new() { Id = "height", Name = "Height", DataType = PortDataType.Number },
            new() { Id = "framesWritten", Name = "Frames Written", DataType = PortDataType.Number },
            new() { Id = "errorMessage", Name = "Error Message", DataType = PortDataType.String }
        },
        Properties = BuildRecordCameraProperties()
    };

    public void Configure(Dictionary<string, object?> properties) =>
        _properties = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var settings = RecordCameraSettings.From(context, _properties);
            ValidateCameraSettings(settings);
            var selectedCameraIndex = ResolveCameraIndex(settings.CameraIndex, settings.CameraNameFilter);

            var outputPath = ResolveVideoOutputPath(context, settings.OutputPath, settings.OutputFolder, settings.FileNameTemplate, settings.Format);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var capture = OpenCamera(selectedCameraIndex);
            capture.Set(CapProp.FrameWidth, settings.Width);
            capture.Set(CapProp.FrameHeight, settings.Height);
            capture.Set(CapProp.Fps, settings.Fps);

            using var firstFrame = capture.QueryFrame();
            if (firstFrame is null || firstFrame.IsEmpty)
                throw new InvalidOperationException($"Unable to read initial camera frame from index {selectedCameraIndex}.");

            var frameWidth = firstFrame.Width;
            var frameHeight = firstFrame.Height;
            using var writer = CreateWriter(outputPath, settings.Codec, settings.Fps, new Size(frameWidth, frameHeight));

            var stopwatch = Stopwatch.StartNew();
            var framesWritten = 0;
            var frameIntervalMs = 1000.0 / settings.Fps;

            while (stopwatch.ElapsedMilliseconds < settings.DurationMs)
            {
                ct.ThrowIfCancellationRequested();
                using var frame = capture.QueryFrame();
                if (frame is null || frame.IsEmpty)
                    continue;

                using var processed = ProcessCameraFrame(frame, settings, stopwatch.Elapsed);
                writer.Write(processed);
                framesWritten++;

                var expectedElapsed = framesWritten * frameIntervalMs;
                var sleepMs = (int)Math.Max(0, expectedElapsed - stopwatch.ElapsedMilliseconds);
                if (sleepMs > 0)
                    await Task.Delay(sleepMs, ct);
            }

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["filePath"] = outputPath,
                ["cameraName"] = $"Camera {selectedCameraIndex}",
                ["width"] = frameWidth,
                ["height"] = frameHeight,
                ["framesWritten"] = framesWritten,
                ["error"] = ""
            });
        }
        catch (OperationCanceledException)
        {
            return NodeResult.Ok("error", new Dictionary<string, object?>
            {
                ["error"] = "Camera recording cancelled"
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Camera recording failed: {ex.Message}");
        }
    }

    private static List<PropertyDefinition> BuildRecordCameraProperties()
    {
        return
        [
            new() { Id = "cameraIndex", Name = "Camera Index", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "cameraNameFilter", Name = "Camera Name Filter", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "width", Name = "Width", Type = PropertyType.Integer, DefaultValue = 1280 },
            new() { Id = "height", Name = "Height", Type = PropertyType.Integer, DefaultValue = 720 },
            new() { Id = "fps", Name = "FPS", Type = PropertyType.Integer, DefaultValue = 24 },
            new() { Id = "durationMs", Name = "Duration (ms)", Type = PropertyType.Integer, DefaultValue = 5000 },
            new() { Id = "outputPath", Name = "Output Path", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filenameTemplate", Name = "Filename Template", Type = PropertyType.String, DefaultValue = "camera_record_{{timestamp}}" },
            new() { Id = "codec", Name = "Codec", Type = PropertyType.String, DefaultValue = "MJPG" },
            new() { Id = "format", Name = "Format", Type = PropertyType.Dropdown, DefaultValue = "avi", Options = ["avi", "mp4"] },
            new() { Id = "mirror", Name = "Mirror", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "cropX", Name = "Crop X", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "cropY", Name = "Crop Y", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "cropWidth", Name = "Crop Width", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "cropHeight", Name = "Crop Height", Type = PropertyType.Integer, DefaultValue = 0 },
            new() { Id = "effect", Name = "Effect", Type = PropertyType.Dropdown, DefaultValue = "none", Options = ["none", "grayscale", "blur"] },
            new() { Id = "overlayTimestamp", Name = "Overlay Timestamp", Type = PropertyType.Boolean, DefaultValue = false }
        ];
    }

    private static void ValidateCameraSettings(RecordCameraSettings settings)
    {
        if (settings.DurationMs <= 0)
            throw new InvalidOperationException("Camera recording durationMs must be greater than zero.");
        if (settings.Fps <= 0)
            throw new InvalidOperationException("Camera recording fps must be greater than zero.");
    }

    private static VideoCapture OpenCamera(int cameraIndex)
    {
        var capture = new VideoCapture(cameraIndex, VideoCapture.API.DShow);
        if (!capture.IsOpened)
            throw new InvalidOperationException($"Unable to open camera index {cameraIndex}.");

        return capture;
    }

    private static int ResolveCameraIndex(int preferredIndex, string cameraNameFilter)
    {
        if (string.IsNullOrWhiteSpace(cameraNameFilter))
            return preferredIndex;

        var filter = cameraNameFilter.Trim();
        if (int.TryParse(filter, out var explicitIndex) && explicitIndex >= 0)
            return explicitIndex;

        for (var index = 0; index <= 5; index++)
        {
            var label = $"Camera {index}";
            if (label.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        throw new InvalidOperationException($"No camera index matched cameraNameFilter '{cameraNameFilter}'. Use cameraIndex or numeric filter.");
    }

    private static Mat ProcessCameraFrame(Mat source, RecordCameraSettings settings, TimeSpan elapsed)
    {
        var output = source.Clone();

        if (settings.Mirror)
            CvInvoke.Flip(output, output, FlipType.Horizontal);

        if (settings.CropWidth > 0 && settings.CropHeight > 0)
        {
            var cropRect = new Rectangle(settings.CropX, settings.CropY, settings.CropWidth, settings.CropHeight);
            cropRect.Intersect(new Rectangle(0, 0, output.Width, output.Height));
            if (cropRect.Width > 0 && cropRect.Height > 0)
                output = new Mat(output, cropRect).Clone();
        }

        if (string.Equals(settings.Effect, "grayscale", StringComparison.OrdinalIgnoreCase))
            CvInvoke.CvtColor(output, output, ColorConversion.Bgr2Gray);
        else if (string.Equals(settings.Effect, "blur", StringComparison.OrdinalIgnoreCase))
            CvInvoke.GaussianBlur(output, output, new Size(7, 7), 1.5);

        if (output.NumberOfChannels == 1)
            CvInvoke.CvtColor(output, output, ColorConversion.Gray2Bgr);

        if (settings.OverlayTimestamp)
        {
            var text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + $" (+{elapsed.TotalSeconds:F1}s)";
            CvInvoke.PutText(output, text, new Point(16, Math.Max(28, output.Height - 18)), FontFace.HersheySimplex, 0.65, new MCvScalar(10, 220, 10), 2);
        }

        return output;
    }

    private static VideoWriter CreateWriter(string outputPath, string codec, int fps, Size frameSize)
    {
        var safeCodec = NormalizeCodec(codec);
        var writer = new VideoWriter(outputPath, VideoWriter.Fourcc(safeCodec[0], safeCodec[1], safeCodec[2], safeCodec[3]), fps, frameSize, true);
        if (!writer.IsOpened)
            throw new InvalidOperationException("Unable to open VideoWriter for camera recording. Check codec/format compatibility.");

        return writer;
    }

    private static string NormalizeCodec(string codec)
    {
        var normalized = (codec ?? "").Trim().ToUpperInvariant();
        return normalized.Length == 4 ? normalized : "MJPG";
    }

    private static string ResolveVideoOutputPath(FlowExecutionContext context, string outputPath, string outputFolder, string fileNameTemplate, string format)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
            return context.ResolveTemplate(outputPath);

        var rootFolder = string.IsNullOrWhiteSpace(outputFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Sidekick")
            : context.ResolveTemplate(outputFolder);

        var template = context.ResolveTemplate(string.IsNullOrWhiteSpace(fileNameTemplate) ? "camera_record_{{timestamp}}" : fileNameTemplate);
        var safe = template.Replace("{{timestamp}}", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        foreach (var invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');

        var extension = string.Equals((format ?? "").Trim(), "mp4", StringComparison.OrdinalIgnoreCase) ? "mp4" : "avi";
        return Path.Combine(rootFolder, $"{safe}.{extension}");
    }

    private sealed record RecordCameraSettings(
        int CameraIndex,
        string CameraNameFilter,
        int Width,
        int Height,
        int Fps,
        int DurationMs,
        string OutputPath,
        string OutputFolder,
        string FileNameTemplate,
        string Codec,
        string Format,
        bool Mirror,
        int CropX,
        int CropY,
        int CropWidth,
        int CropHeight,
        string Effect,
        bool OverlayTimestamp)
    {
        public static RecordCameraSettings From(FlowExecutionContext context, Dictionary<string, object?> properties)
        {
            return new RecordCameraSettings(
                CameraIndex: NodeValueHelper.GetInt(properties, "cameraIndex", 0),
                CameraNameFilter: NodeValueHelper.ResolveTemplateProperty(context, properties, "cameraNameFilter"),
                Width: Math.Max(1, NodeValueHelper.GetInt(properties, "width", 1280)),
                Height: Math.Max(1, NodeValueHelper.GetInt(properties, "height", 720)),
                Fps: NodeValueHelper.GetInt(properties, "fps", 24),
                DurationMs: NodeValueHelper.GetInt(properties, "durationMs", 5000),
                OutputPath: NodeValueHelper.ResolveTemplateProperty(context, properties, "outputPath"),
                OutputFolder: NodeValueHelper.ResolveTemplateProperty(context, properties, "outputFolder"),
                FileNameTemplate: NodeValueHelper.ResolveTemplateProperty(context, properties, "filenameTemplate", "camera_record_{{timestamp}}"),
                Codec: NodeValueHelper.ResolveTemplateProperty(context, properties, "codec", "MJPG"),
                Format: NodeValueHelper.ResolveTemplateProperty(context, properties, "format", "avi"),
                Mirror: NodeValueHelper.GetBool(properties, "mirror", false),
                CropX: NodeValueHelper.GetInt(properties, "cropX", 0),
                CropY: NodeValueHelper.GetInt(properties, "cropY", 0),
                CropWidth: NodeValueHelper.GetInt(properties, "cropWidth", 0),
                CropHeight: NodeValueHelper.GetInt(properties, "cropHeight", 0),
                Effect: NodeValueHelper.ResolveTemplateProperty(context, properties, "effect", "none"),
                OverlayTimestamp: NodeValueHelper.GetBool(properties, "overlayTimestamp", false));
        }
    }
}
