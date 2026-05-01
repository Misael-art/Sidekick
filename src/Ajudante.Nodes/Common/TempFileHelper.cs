namespace Ajudante.Nodes.Common;

internal static class TempFileHelper
{
    public static string CreateScratchFile(string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(Path.GetTempPath(), $"sidekick_{Guid.NewGuid():N}{ext}");
    }

    public static void EnsureOutputFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return;
        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);
    }

    public static string BuildOutputPath(string folder, string template, string sourcePath, string extension)
    {
        EnsureOutputFolder(folder);
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        var resolvedTemplate = string.IsNullOrWhiteSpace(template) ? "{{name}}" : template;
        var fileName = resolvedTemplate
            .Replace("{{name}}", sourceName)
            .Replace("{{ext}}", ext.TrimStart('.'))
            .Replace("{{timestamp}}", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        if (!Path.HasExtension(fileName))
            fileName += ext;
        return Path.Combine(folder, fileName);
    }

    public static string EnsureUnique(string path)
    {
        if (!File.Exists(path))
            return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 10000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }
        throw new IOException($"Cannot generate unique file name for {path}");
    }
}
