using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Text.Json;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Nodes.Actions;

namespace Ajudante.Nodes.Tests;

public class ImageNodesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AjudanteTests", Guid.NewGuid().ToString("N"));

    public ImageNodesTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static FlowExecutionContext Ctx() => new(new Flow { Name = "T" }, CancellationToken.None);

    private string MakeBitmap(string name, int w, int h, ImageFormat fmt)
    {
        var path = Path.Combine(_root, name);
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Blue);
        bmp.Save(path, fmt);
        return path;
    }

    [Fact]
    public void ImageConvertNode_Definition_HasCorrectTypeId()
    {
        var n = new ImageConvertNode();
        Assert.Equal("action.imageConvert", n.Definition.TypeId);
        Assert.Equal(NodeCategory.Action, n.Definition.Category);
        var fmt = n.Definition.Properties.Find(p => p.Id == "format")!;
        Assert.Equal(PropertyType.Dropdown, fmt.Type);
        Assert.Contains("png", fmt.Options!);
        Assert.Contains("webp", fmt.Options!);
    }

    [Fact]
    public async Task ImageConvertNode_PngToJpg_Succeeds()
    {
        var src = MakeBitmap("src.png", 50, 40, ImageFormat.Png);
        var node = new ImageConvertNode();
        node.Configure(new()
        {
            ["inputFile"] = src,
            ["outputFolder"] = _root,
            ["format"] = "jpg",
            ["overwrite"] = true,
            ["quality"] = 80
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success, r.Error);
        var outputPath = (string?)r.Outputs!["outputPath"]!;
        Assert.True(File.Exists(outputPath));
        Assert.EndsWith(".jpg", outputPath);
    }

    [Fact]
    public async Task ImageConvertNode_MissingInput_Fails()
    {
        var node = new ImageConvertNode();
        node.Configure(new() { ["inputFile"] = Path.Combine(_root, "nope.png"), ["format"] = "png" });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.False(r.Success);
        Assert.Contains("not found", r.Error);
    }

    [Fact]
    public async Task ImageResizeNode_Fit_KeepsAspectRatio()
    {
        var src = MakeBitmap("src.png", 200, 100, ImageFormat.Png);
        var node = new ImageResizeNode();
        node.Configure(new()
        {
            ["inputFile"] = src,
            ["outputFolder"] = _root,
            ["width"] = 100,
            ["height"] = 100,
            ["mode"] = "fit",
            ["keepAspectRatio"] = true
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success, r.Error);
        Assert.Equal(100, Convert.ToInt32(r.Outputs!["width"]));
        Assert.Equal(50, Convert.ToInt32(r.Outputs!["height"]));
    }

    [Fact]
    public async Task ImageCompressNode_ProducesSmallerOrEqualOutput()
    {
        var src = MakeBitmap("src.bmp", 200, 200, ImageFormat.Bmp);
        var node = new ImageCompressNode();
        node.Configure(new()
        {
            ["inputFile"] = src,
            ["outputFolder"] = _root,
            ["quality"] = 50
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success, r.Error);
        var orig = Convert.ToInt64(r.Outputs!["originalSizeBytes"]);
        var nw = Convert.ToInt64(r.Outputs!["newSizeBytes"]);
        Assert.True(nw <= orig, $"new {nw} should be <= original {orig}");
    }
}

public class ArchiveNodesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AjudanteTests", Guid.NewGuid().ToString("N"));

    public ArchiveNodesTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static FlowExecutionContext Ctx() => new(new Flow { Name = "T" }, CancellationToken.None);

    [Fact]
    public void ArchiveCreateNode_Definition_HasCorrectTypeId()
    {
        var n = new ArchiveCreateNode();
        Assert.Equal("action.archiveCreate", n.Definition.TypeId);
        var fmt = n.Definition.Properties.Find(p => p.Id == "format")!;
        Assert.Equal(PropertyType.Dropdown, fmt.Type);
        Assert.Contains("zip", fmt.Options!);
        Assert.Contains("7z", fmt.Options!);
    }

    [Fact]
    public async Task ArchiveCreateExtract_RoundTripZip_Succeeds()
    {
        var srcDir = Path.Combine(_root, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(srcDir, "b.txt"), "world");

        var zipPath = Path.Combine(_root, "out.zip");
        var create = new ArchiveCreateNode();
        create.Configure(new()
        {
            ["sourcePath"] = srcDir,
            ["outputFile"] = zipPath,
            ["format"] = "zip",
            ["compressionLevel"] = "fast",
            ["overwrite"] = true
        });
        var r = await create.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success, r.Error);
        Assert.True(File.Exists(zipPath));

        var extractDir = Path.Combine(_root, "out");
        var extract = new ArchiveExtractNode();
        extract.Configure(new()
        {
            ["inputFile"] = zipPath,
            ["outputFolder"] = extractDir,
            ["overwrite"] = true
        });
        var r2 = await extract.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r2.Success, r2.Error);
        Assert.True(File.Exists(Path.Combine(extractDir, "a.txt")));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(extractDir, "a.txt")));
    }

    [Fact]
    public async Task ArchiveCreateNode_RefusesOverwriteWithoutFlag()
    {
        var srcDir = Path.Combine(_root, "src");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "a.txt"), "x");

        var zipPath = Path.Combine(_root, "exists.zip");
        File.WriteAllText(zipPath, "stale");

        var create = new ArchiveCreateNode();
        create.Configure(new()
        {
            ["sourcePath"] = srcDir,
            ["outputFile"] = zipPath,
            ["format"] = "zip",
            ["overwrite"] = false
        });
        var r = await create.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.False(r.Success);
        Assert.Contains("Overwrite", r.Error);
    }

    [Fact]
    public async Task ArchiveExtract_UnsupportedExtension_Fails()
    {
        var bogus = Path.Combine(_root, "weird.xyz");
        File.WriteAllText(bogus, "x");
        var node = new ArchiveExtractNode();
        node.Configure(new()
        {
            ["inputFile"] = bogus,
            ["outputFolder"] = _root
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.False(r.Success);
    }
}

public class FileUtilityNodesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AjudanteTests", Guid.NewGuid().ToString("N"));

    public FileUtilityNodesTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static FlowExecutionContext Ctx() => new(new Flow { Name = "T" }, CancellationToken.None);

    [Fact]
    public async Task FileHashNode_ComputesSha256()
    {
        var path = Path.Combine(_root, "h.txt");
        File.WriteAllText(path, "abc");
        var node = new FileHashNode();
        node.Configure(new()
        {
            ["inputFile"] = path,
            ["algorithm"] = "SHA256"
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        // SHA256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", (string?)r.Outputs!["hash"]);
    }

    [Fact]
    public async Task FindDuplicateFilesNode_FindsIdenticalContent()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "same");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "same");
        File.WriteAllText(Path.Combine(_root, "c.txt"), "different");
        var node = new FindDuplicateFilesNode();
        node.Configure(new()
        {
            ["folder"] = _root,
            ["filter"] = "*.txt",
            ["recursive"] = false,
            ["minSizeBytes"] = 1
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal(1, Convert.ToInt32(r.Outputs!["groupCount"]));
        Assert.Equal(2, Convert.ToInt32(r.Outputs!["duplicateFileCount"]));
    }

    [Fact]
    public async Task BatchRenameNode_DryRunByDefault_DoesNotModify()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "y");
        var node = new BatchRenameNode();
        node.Configure(new()
        {
            ["folder"] = _root,
            ["filter"] = "*.txt",
            ["mode"] = "prefix",
            ["value"] = "pre_"
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.True((bool)r.Outputs!["dryRun"]!);
        Assert.Equal(2, Convert.ToInt32(r.Outputs!["plannedCount"]));
        // files unchanged
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "b.txt")));
    }

    [Fact]
    public async Task BatchRenameNode_ApplyRequiresConfirm()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");
        var node = new BatchRenameNode();
        node.Configure(new()
        {
            ["folder"] = _root,
            ["filter"] = "*.txt",
            ["mode"] = "prefix",
            ["value"] = "pre_",
            ["dryRun"] = false,
            ["confirmApply"] = false
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.False(r.Success);
        Assert.Contains("confirmApply", r.Error);
    }

    [Fact]
    public async Task BatchRenameNode_PrefixApplied_WhenConfirmed()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");
        var node = new BatchRenameNode();
        node.Configure(new()
        {
            ["folder"] = _root,
            ["filter"] = "*.txt",
            ["mode"] = "prefix",
            ["value"] = "pre_",
            ["dryRun"] = false,
            ["confirmApply"] = true
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal(1, Convert.ToInt32(r.Outputs!["renamedCount"]));
        Assert.True(File.Exists(Path.Combine(_root, "pre_a.txt")));
    }

    [Fact]
    public async Task CleanFolderNode_DryRun_LogsCandidatesOnly()
    {
        var oldPath = Path.Combine(_root, "old.txt");
        File.WriteAllText(oldPath, "x");
        File.SetLastWriteTimeUtc(oldPath, DateTime.UtcNow.AddDays(-60));
        File.WriteAllText(Path.Combine(_root, "new.txt"), "y");
        var node = new CleanFolderNode();
        node.Configure(new()
        {
            ["folder"] = _root,
            ["filter"] = "*.txt",
            ["olderThanDays"] = 30,
            ["dryRun"] = true
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal(1, Convert.ToInt32(r.Outputs!["candidateCount"]));
        Assert.True(File.Exists(oldPath));
    }

    [Fact]
    public async Task OrganizeFolderNode_DryRun_Plans()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "b.jpg"), "y");
        var node = new OrganizeFolderNode();
        node.Configure(new() { ["folder"] = _root, ["groupBy"] = "extension" });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal(2, Convert.ToInt32(r.Outputs!["plannedCount"]));
    }
}

public class DataUtilityNodesTests
{
    private static FlowExecutionContext Ctx() => new(new Flow { Name = "T" }, CancellationToken.None);

    [Fact]
    public async Task ClipboardTransform_Uppercase_Works()
    {
        var node = new ClipboardTransformNode();
        node.Configure(new() { ["input"] = "hello", ["mode"] = "uppercase" });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal("HELLO", (string?)r.Outputs!["result"]);
    }

    [Fact]
    public async Task ClipboardTransform_RegexReplace_Works()
    {
        var node = new ClipboardTransformNode();
        node.Configure(new()
        {
            ["input"] = "abc123",
            ["mode"] = "regexReplace",
            ["find"] = "\\d+",
            ["replacement"] = "X"
        });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        Assert.Equal("abcX", (string?)r.Outputs!["result"]);
    }

    [Fact]
    public async Task ClipboardTransform_JsonFormat_Pretties()
    {
        var node = new ClipboardTransformNode();
        node.Configure(new() { ["input"] = "{\"a\":1}", ["mode"] = "jsonFormat" });
        var r = await node.ExecuteAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Success);
        var result = (string?)r.Outputs!["result"];
        Assert.Contains("\n", result);
    }
}

public class MediaPdfDefinitionTests
{
    [Fact]
    public void VideoConvertNode_HasFormatDropdown()
    {
        var n = new VideoConvertNode();
        Assert.Equal("action.videoConvert", n.Definition.TypeId);
        var fmt = n.Definition.Properties.Find(p => p.Id == "format")!;
        Assert.Equal(PropertyType.Dropdown, fmt.Type);
        Assert.Contains("mp4", fmt.Options!);
        Assert.Contains("webm", fmt.Options!);
    }

    [Fact]
    public void AudioConvertNode_HasFormatDropdown()
    {
        var n = new AudioConvertNode();
        var fmt = n.Definition.Properties.Find(p => p.Id == "format")!;
        Assert.Contains("mp3", fmt.Options!);
        Assert.Contains("flac", fmt.Options!);
    }

    [Fact]
    public void PdfMergeNode_HasOutputProp()
    {
        var n = new PdfMergeNode();
        Assert.Equal("action.pdfMerge", n.Definition.TypeId);
        Assert.Contains(n.Definition.Properties, p => p.Id == "outputFile");
    }

    [Fact]
    public void PdfSplitNode_ModeDropdown()
    {
        var n = new PdfSplitNode();
        var mode = n.Definition.Properties.Find(p => p.Id == "mode")!;
        Assert.Equal(PropertyType.Dropdown, mode.Type);
        Assert.Contains("byPage", mode.Options!);
        Assert.Contains("range", mode.Options!);
    }

    [Fact]
    public void DocumentConvertNode_FormatDropdown()
    {
        var n = new DocumentConvertNode();
        var fmt = n.Definition.Properties.Find(p => p.Id == "outputFormat")!;
        Assert.Equal(PropertyType.Dropdown, fmt.Type);
        Assert.Contains("pdf", fmt.Options!);
        Assert.Contains("txt", fmt.Options!);
    }

    [Fact]
    public void FolderSizeChangedTrigger_DirectionDropdown()
    {
        var n = new Ajudante.Nodes.Triggers.FolderSizeChangedTriggerNode();
        var dir = n.Definition.Properties.Find(p => p.Id == "direction")!;
        Assert.Equal(PropertyType.Dropdown, dir.Type);
        Assert.Contains("any", dir.Options!);
    }

    [Fact]
    public void DiskSpaceLowTrigger_ModeDropdown()
    {
        var n = new Ajudante.Nodes.Triggers.DiskSpaceLowTriggerNode();
        var mode = n.Definition.Properties.Find(p => p.Id == "thresholdMode")!;
        Assert.Equal(PropertyType.Dropdown, mode.Type);
        Assert.Contains("absolute", mode.Options!);
        Assert.Contains("percent", mode.Options!);
    }
}

public class TextToPdfTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AjudanteTests", Guid.NewGuid().ToString("N"));

    public TextToPdfTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task TextToPdf_ProducesPdf()
    {
        var node = new TextToPdfNode();
        var output = Path.Combine(_root, "out.pdf");
        node.Configure(new()
        {
            ["text"] = "Line one\nLine two",
            ["outputFile"] = output,
            ["fontSize"] = 12
        });
        var ctx = new FlowExecutionContext(new Flow { Name = "T" }, CancellationToken.None);
        var r = await node.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(r.Success, r.Error);
        Assert.True(File.Exists(output));
        Assert.True(new FileInfo(output).Length > 100);
    }

    [Fact]
    public async Task ImagesToPdf_ProducesPdfFromOnePng()
    {
        var img = Path.Combine(_root, "tile.png");
        using (var bmp = new Bitmap(50, 50))
        {
            using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Red);
            bmp.Save(img, ImageFormat.Png);
        }
        var output = Path.Combine(_root, "out.pdf");
        var node = new ImagesToPdfNode();
        node.Configure(new()
        {
            ["inputFiles"] = img,
            ["outputFile"] = output
        });
        var ctx = new FlowExecutionContext(new Flow { Name = "T" }, CancellationToken.None);
        var r = await node.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(r.Success, r.Error);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public async Task PdfMergeAndSplit_RoundTrip()
    {
        // Build two simple text PDFs first
        var pdf1 = Path.Combine(_root, "a.pdf");
        var pdf2 = Path.Combine(_root, "b.pdf");
        var ctx = new FlowExecutionContext(new Flow { Name = "T" }, CancellationToken.None);

        var t1 = new TextToPdfNode();
        t1.Configure(new() { ["text"] = "page A", ["outputFile"] = pdf1 });
        var r1 = await t1.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(r1.Success, r1.Error);

        var t2 = new TextToPdfNode();
        t2.Configure(new() { ["text"] = "page B", ["outputFile"] = pdf2 });
        var r2 = await t2.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(r2.Success, r2.Error);

        var merged = Path.Combine(_root, "m.pdf");
        var merge = new PdfMergeNode();
        merge.Configure(new()
        {
            ["inputFiles"] = $"{pdf1};{pdf2}",
            ["outputFile"] = merged,
            ["overwrite"] = true
        });
        var rm = await merge.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(rm.Success, rm.Error);
        Assert.True(File.Exists(merged));
        Assert.Equal(2, Convert.ToInt32(rm.Outputs!["pageCount"]));

        var splitDir = Path.Combine(_root, "split");
        var split = new PdfSplitNode();
        split.Configure(new()
        {
            ["inputFile"] = merged,
            ["mode"] = "byPage",
            ["outputFolder"] = splitDir
        });
        var rs = await split.ExecuteAsync(ctx, CancellationToken.None);
        Assert.True(rs.Success, rs.Error);
        Assert.Equal(2, Convert.ToInt32(rs.Outputs!["partCount"]));
    }
}

public class ToolDetectorMissingTests
{
    [Fact]
    public async Task VideoConvert_NoFfmpeg_FailsClearly()
    {
        // We can't reliably have FFmpeg absent in CI, so this test is best-effort:
        // skip if ffmpeg actually available.
        if (Ajudante.Nodes.Common.ToolDetector.FindFfmpeg() != null)
            return;

        var node = new VideoConvertNode();
        node.Configure(new()
        {
            ["inputFile"] = Path.Combine(Path.GetTempPath(), "no_such.mp4"),
            ["outputFolder"] = Path.GetTempPath(),
            ["format"] = "mp4"
        });
        var ctx = new FlowExecutionContext(new Flow { Name = "T" }, CancellationToken.None);
        var r = await node.ExecuteAsync(ctx, CancellationToken.None);
        Assert.False(r.Success);
        Assert.Contains("FFmpeg", r.Error);
    }
}
