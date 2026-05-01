using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Nodes.Common;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace Ajudante.Nodes.Actions;

[NodeInfo(
    TypeId = "action.pdfMerge",
    DisplayName = "PDF Merge",
    Category = NodeCategory.Action,
    Description = "Merge multiple PDFs into a single file",
    Color = "#22C55E")]
public sealed class PdfMergeNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.pdfMerge",
        DisplayName = "PDF Merge",
        Category = NodeCategory.Action,
        Description = "Combine multiple PDFs in order. Input is ';'-separated list of paths.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFiles", Name = "Input PDFs (';'-separated)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "outputFile", Name = "Output PDF", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "overwrite", Name = "Overwrite Existing", Type = PropertyType.Boolean, DefaultValue = false },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var inputs = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFiles"));
            var output = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFile"));
            var overwrite = NodeValueHelper.GetBool(_props, "overwrite", false);
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            var paths = inputs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (paths.Length == 0)
                return Task.FromResult(NodeResult.Fail("Provide at least one PDF path."));
            if (string.IsNullOrWhiteSpace(output))
                return Task.FromResult(NodeResult.Fail("Output PDF path required."));

            if (File.Exists(output) && !overwrite)
                return Task.FromResult(NodeResult.Fail($"Output exists. Set Overwrite = true: {output}"));

            var outputDir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDir))
                TempFileHelper.EnsureOutputFolder(outputDir);

            using var merged = new PdfDocument();
            foreach (var p in paths)
            {
                if (!File.Exists(p))
                    return Task.FromResult(NodeResult.Fail($"Input PDF not found: {p}"));
                using var src = PdfReader.Open(p, PdfDocumentOpenMode.Import);
                for (var i = 0; i < src.PageCount; i++)
                    merged.AddPage(src.Pages[i]);
            }
            merged.Save(output);

            NodeValueHelper.SetVariableIfRequested(context, outVar, output);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = output,
                ["pageCount"] = merged.PageCount
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"PDF merge failed: {ex.Message}"));
        }
    }
}

[NodeInfo(
    TypeId = "action.pdfSplit",
    DisplayName = "PDF Split",
    Category = NodeCategory.Action,
    Description = "Split a PDF by page or page range",
    Color = "#22C55E")]
public sealed class PdfSplitNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.pdfSplit",
        DisplayName = "PDF Split",
        Category = NodeCategory.Action,
        Description = "Split a PDF. Modes: byPage (one PDF per page), range (eg 1-3,5,7-10).",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input PDF", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "mode", Name = "Mode", Type = PropertyType.Dropdown, DefaultValue = "byPage", Options = new[] { "byPage", "range" } },
            new() { Id = "range", Name = "Range (mode=range, eg 1-3,5)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputVariable", Name = "Output Files Variable (JSON)", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var mode = NodeValueHelper.GetString(_props, "mode", "byPage").ToLowerInvariant();
            var rangeRaw = NodeValueHelper.GetString(_props, "range");
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input)) return Task.FromResult(NodeResult.Fail($"Input PDF not found: {input}"));
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();
            TempFileHelper.EnsureOutputFolder(folder);

            using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            var baseName = Path.GetFileNameWithoutExtension(input);
            var outputs = new List<string>();

            if (mode == "bypage")
            {
                for (var i = 0; i < source.PageCount; i++)
                {
                    using var part = new PdfDocument();
                    part.AddPage(source.Pages[i]);
                    var path = TempFileHelper.EnsureUnique(Path.Combine(folder, $"{baseName}_{i + 1}.pdf"));
                    part.Save(path);
                    outputs.Add(path);
                }
            }
            else
            {
                var ranges = ParseRanges(rangeRaw, source.PageCount);
                if (ranges.Count == 0)
                    return Task.FromResult(NodeResult.Fail("Range mode requires a non-empty range expression."));
                var idx = 1;
                foreach (var range in ranges)
                {
                    using var part = new PdfDocument();
                    foreach (var pageIdx in range)
                        part.AddPage(source.Pages[pageIdx]);
                    var path = TempFileHelper.EnsureUnique(Path.Combine(folder, $"{baseName}_part{idx}.pdf"));
                    part.Save(path);
                    outputs.Add(path);
                    idx++;
                }
            }

            var json = System.Text.Json.JsonSerializer.Serialize(outputs);
            NodeValueHelper.SetVariableIfRequested(context, outVar, json);

            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputFiles"] = outputs,
                ["partCount"] = outputs.Count
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"PDF split failed: {ex.Message}"));
        }
    }

    private static List<List<int>> ParseRanges(string spec, int pageCount)
    {
        var result = new List<List<int>>();
        if (string.IsNullOrWhiteSpace(spec)) return result;
        foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = new List<int>();
            if (part.Contains('-'))
            {
                var bits = part.Split('-', 2);
                if (int.TryParse(bits[0], out var a) && int.TryParse(bits[1], out var b))
                {
                    var start = Math.Clamp(Math.Min(a, b), 1, pageCount);
                    var end = Math.Clamp(Math.Max(a, b), 1, pageCount);
                    for (var i = start; i <= end; i++) range.Add(i - 1);
                }
            }
            else if (int.TryParse(part, out var p))
            {
                if (p >= 1 && p <= pageCount) range.Add(p - 1);
            }
            if (range.Count > 0) result.Add(range);
        }
        return result;
    }
}

[NodeInfo(
    TypeId = "action.imagesToPdf",
    DisplayName = "Images To PDF",
    Category = NodeCategory.Action,
    Description = "Combine images into a single PDF",
    Color = "#22C55E")]
public sealed class ImagesToPdfNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.imagesToPdf",
        DisplayName = "Images To PDF",
        Category = NodeCategory.Action,
        Description = "Build a PDF from images. Either ';'-separated paths or a folder.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFiles", Name = "Input Files (';' separated, optional if folder set)", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "inputFolder", Name = "Input Folder (optional)", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "filter", Name = "Folder Filter", Type = PropertyType.String, DefaultValue = "*.jpg;*.png" },
            new() { Id = "outputFile", Name = "Output PDF", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "pageSize", Name = "Page Size", Type = PropertyType.Dropdown, DefaultValue = "A4", Options = new[] { "A4", "A3", "A5", "Letter", "Legal" } },
            new() { Id = "orientation", Name = "Orientation", Type = PropertyType.Dropdown, DefaultValue = "Portrait", Options = new[] { "Portrait", "Landscape" } },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var inputs = NodeValueHelper.GetString(_props, "inputFiles");
            var inputFolder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFolder"));
            var filter = NodeValueHelper.GetString(_props, "filter", "*.jpg;*.png");
            var output = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFile"));
            var pageSizeKey = NodeValueHelper.GetString(_props, "pageSize", "A4");
            var orientation = NodeValueHelper.GetString(_props, "orientation", "Portrait");
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (string.IsNullOrWhiteSpace(output))
                return Task.FromResult(NodeResult.Fail("Output PDF path required."));

            var paths = new List<string>();
            if (!string.IsNullOrWhiteSpace(inputs))
                paths.AddRange(inputs.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (!string.IsNullOrWhiteSpace(inputFolder) && Directory.Exists(inputFolder))
            {
                foreach (var pat in filter.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    paths.AddRange(Directory.GetFiles(inputFolder, pat));
            }
            paths = paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            if (paths.Count == 0)
                return Task.FromResult(NodeResult.Fail("No input images found."));

            var outputDir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDir))
                TempFileHelper.EnsureOutputFolder(outputDir);

            using var doc = new PdfDocument();
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
                var page = doc.AddPage();
                page.Size = ResolvePageSize(pageSizeKey);
                page.Orientation = orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase)
                    ? PdfSharpCore.PageOrientation.Landscape
                    : PdfSharpCore.PageOrientation.Portrait;
                using var gfx = XGraphics.FromPdfPage(page);
                using var img = XImage.FromFile(p);
                var ratio = Math.Min(page.Width / img.PixelWidth, page.Height / img.PixelHeight);
                var w = img.PixelWidth * ratio;
                var h = img.PixelHeight * ratio;
                var x = (page.Width - w) / 2;
                var y = (page.Height - h) / 2;
                gfx.DrawImage(img, x, y, w, h);
            }
            doc.Save(output);

            NodeValueHelper.SetVariableIfRequested(context, outVar, output);
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = output,
                ["pageCount"] = doc.PageCount
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Images to PDF failed: {ex.Message}"));
        }
    }

    private static PdfSharpCore.PageSize ResolvePageSize(string key) => key.ToUpperInvariant() switch
    {
        "A3" => PdfSharpCore.PageSize.A3,
        "A5" => PdfSharpCore.PageSize.A5,
        "LETTER" => PdfSharpCore.PageSize.Letter,
        "LEGAL" => PdfSharpCore.PageSize.Legal,
        _ => PdfSharpCore.PageSize.A4
    };
}

[NodeInfo(
    TypeId = "action.textToPdf",
    DisplayName = "Text To PDF",
    Category = NodeCategory.Action,
    Description = "Render plain text as a PDF",
    Color = "#22C55E")]
public sealed class TextToPdfNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.textToPdf",
        DisplayName = "Text To PDF",
        Category = NodeCategory.Action,
        Description = "Build a simple text PDF. Provide text directly or a path.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "text", Name = "Text (supports {{variables}})", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "inputFile", Name = "Or Input Text File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFile", Name = "Output PDF", Type = PropertyType.String, DefaultValue = "" },
            new() { Id = "fontSize", Name = "Font Size", Type = PropertyType.Integer, DefaultValue = 11 },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var text = context.ResolveTemplate(NodeValueHelper.GetString(_props, "text"));
            var inputFile = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var output = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFile"));
            var fontSize = Math.Max(6, NodeValueHelper.GetInt(_props, "fontSize", 11));
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (string.IsNullOrEmpty(text) && File.Exists(inputFile))
                text = File.ReadAllText(inputFile);
            if (string.IsNullOrEmpty(text))
                return Task.FromResult(NodeResult.Fail("Text or input file required."));
            if (string.IsNullOrWhiteSpace(output))
                return Task.FromResult(NodeResult.Fail("Output PDF path required."));

            var outputDir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDir))
                TempFileHelper.EnsureOutputFolder(outputDir);

            using var doc = new PdfDocument();
            var font = new XFont("Arial", fontSize);
            var lines = text.Replace("\r\n", "\n").Split('\n');
            const double margin = 40;
            int lineIdx = 0;
            while (lineIdx < lines.Length)
            {
                var page = doc.AddPage();
                page.Size = PdfSharpCore.PageSize.A4;
                using var gfx = XGraphics.FromPdfPage(page);
                double y = margin;
                while (lineIdx < lines.Length && y + fontSize < page.Height - margin)
                {
                    gfx.DrawString(lines[lineIdx], font, XBrushes.Black, new XRect(margin, y, page.Width - 2 * margin, fontSize + 2), XStringFormats.TopLeft);
                    y += fontSize + 4;
                    lineIdx++;
                }
            }
            doc.Save(output);

            NodeValueHelper.SetVariableIfRequested(context, outVar, output);
            return Task.FromResult(NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = output,
                ["pageCount"] = doc.PageCount
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(NodeResult.Fail($"Text to PDF failed: {ex.Message}"));
        }
    }
}

[NodeInfo(
    TypeId = "action.documentConvert",
    DisplayName = "Document Convert",
    Category = NodeCategory.Action,
    Description = "Convert documents (txt/md/html/pdf) — LibreOffice for office formats",
    Color = "#22C55E")]
public sealed class DocumentConvertNode : IActionNode
{
    private Dictionary<string, object?> _props = new();

    public string Id { get; set; } = "";

    public NodeDefinition Definition => new()
    {
        TypeId = "action.documentConvert",
        DisplayName = "Document Convert",
        Category = NodeCategory.Action,
        Description = "Convert plain documents. txt↔md↔html native; office formats and pdf-from-doc require LibreOffice.",
        Color = "#22C55E",
        InputPorts = new() { new() { Id = "in", Name = "In", DataType = PortDataType.Flow } },
        OutputPorts = new() { new() { Id = "out", Name = "Out", DataType = PortDataType.Flow } },
        Properties = new()
        {
            new() { Id = "inputFile", Name = "Input File", Type = PropertyType.FilePath, DefaultValue = "" },
            new() { Id = "outputFolder", Name = "Output Folder", Type = PropertyType.FolderPath, DefaultValue = "" },
            new() { Id = "outputFormat", Name = "Output Format", Type = PropertyType.Dropdown, DefaultValue = "txt", Options = new[] { "txt", "md", "html", "pdf" } },
            new() { Id = "outputVariable", Name = "Output Path Variable", Type = PropertyType.String, DefaultValue = "" }
        }
    };

    public void Configure(Dictionary<string, object?> properties) => _props = new(properties);

    public async Task<NodeResult> ExecuteAsync(FlowExecutionContext context, CancellationToken ct)
    {
        try
        {
            var input = context.ResolveTemplate(NodeValueHelper.GetString(_props, "inputFile"));
            var folder = context.ResolveTemplate(NodeValueHelper.GetString(_props, "outputFolder"));
            var format = NodeValueHelper.GetString(_props, "outputFormat", "txt").ToLowerInvariant();
            var outVar = NodeValueHelper.GetString(_props, "outputVariable");

            if (!File.Exists(input)) return NodeResult.Fail($"Input file not found: {input}");
            if (string.IsNullOrWhiteSpace(folder)) folder = Path.GetDirectoryName(input) ?? Path.GetTempPath();
            TempFileHelper.EnsureOutputFolder(folder);

            var inExt = Path.GetExtension(input).ToLowerInvariant();
            var outputPath = Path.Combine(folder, Path.GetFileNameWithoutExtension(input) + "." + format);
            outputPath = TempFileHelper.EnsureUnique(outputPath);

            var simple = new[] { ".txt", ".md", ".html", ".htm" };
            if (simple.Contains(inExt) && (format == "txt" || format == "md" || format == "html"))
            {
                var content = File.ReadAllText(input);
                if ((inExt == ".html" || inExt == ".htm") && format == "txt")
                {
                    content = StripHtml(content);
                }
                else if (format == "html" && (inExt == ".txt" || inExt == ".md"))
                {
                    content = $"<!doctype html><html><body><pre>{System.Net.WebUtility.HtmlEncode(content)}</pre></body></html>";
                }
                File.WriteAllText(outputPath, content);
                NodeValueHelper.SetVariableIfRequested(context, outVar, outputPath);
                return NodeResult.Ok("out", new Dictionary<string, object?> { ["outputPath"] = outputPath });
            }

            // Anything else (docx, odt, .doc, or → pdf from non-text): need LibreOffice
            var soffice = ToolDetector.FindLibreOffice();
            if (soffice == null)
                return NodeResult.Fail("LibreOffice (soffice.exe) is required for this conversion. Install LibreOffice and ensure it is on PATH or in C:\\Program Files\\LibreOffice\\program\\.");

            var args = $"--headless --convert-to {format} --outdir \"{folder}\" \"{input}\"";
            var outcome = await ProcessRunner.RunAsync(soffice, args, ct).ConfigureAwait(false);
            if (outcome.ExitCode != 0)
                return NodeResult.Fail($"LibreOffice conversion failed (exit {outcome.ExitCode}): {outcome.StdErr}");

            var produced = Path.Combine(folder, Path.GetFileNameWithoutExtension(input) + "." + format);
            NodeValueHelper.SetVariableIfRequested(context, outVar, produced);

            return NodeResult.Ok("out", new Dictionary<string, object?>
            {
                ["outputPath"] = produced
            });
        }
        catch (Exception ex)
        {
            return NodeResult.Fail($"Document convert failed: {ex.Message}");
        }
    }

    private static string StripHtml(string html)
    {
        var noTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(noTags);
    }
}
