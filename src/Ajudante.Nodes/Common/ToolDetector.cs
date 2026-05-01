using System.Collections.Concurrent;

namespace Ajudante.Nodes.Common;

public static class ToolDetector
{
    private static readonly ConcurrentDictionary<string, string?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static string? FindFfmpeg() => Find("ffmpeg.exe", new[]
    {
        @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
        @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
        @"C:\ffmpeg\bin\ffmpeg.exe"
    });

    public static string? FindFfprobe() => Find("ffprobe.exe", new[]
    {
        @"C:\Program Files\ffmpeg\bin\ffprobe.exe",
        @"C:\Program Files (x86)\ffmpeg\bin\ffprobe.exe",
        @"C:\ffmpeg\bin\ffprobe.exe"
    });

    public static string? FindSevenZip() => Find("7z.exe", new[]
    {
        @"C:\Program Files\7-Zip\7z.exe",
        @"C:\Program Files (x86)\7-Zip\7z.exe"
    });

    public static string? FindLibreOffice() => Find("soffice.exe", new[]
    {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    });

    public static void ResetCache() => Cache.Clear();

    private static string? Find(string exeName, string[] commonPaths)
    {
        return Cache.GetOrAdd(exeName, key =>
        {
            var fromPath = SearchInPath(key);
            if (fromPath != null)
                return fromPath;

            foreach (var p in commonPaths)
            {
                if (File.Exists(p))
                    return p;
            }

            return null;
        });
    }

    private static string? SearchInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), exeName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // skip invalid path entries
            }
        }
        return null;
    }
}
