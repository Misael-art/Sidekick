using System.IO;

namespace Ajudante.Nodes.Common;

internal static class ImageTemplateResolver
{
    public static byte[]? Resolve(object? value)
    {
        switch (value)
        {
            case byte[] bytes when bytes.Length > 0:
                return bytes;
            case string text when !string.IsNullOrWhiteSpace(text):
                return TryReadTemplateBytes(text);
            case Dictionary<string, object?> payload:
                if (TryGetString(payload, "imageBase64") is string base64Bytes)
                    return TryReadTemplateBytes(base64Bytes);

                if (TryGetString(payload, "imagePath") is string imagePath)
                    return TryReadTemplateBytes(imagePath);

                return null;
            default:
                return null;
        }
    }

    private static byte[]? TryReadTemplateBytes(string value)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            if (File.Exists(value))
                return File.ReadAllBytes(value);

            return null;
        }
    }

    private static string? TryGetString(Dictionary<string, object?> values, string key)
    {
        return values.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;
    }
}
