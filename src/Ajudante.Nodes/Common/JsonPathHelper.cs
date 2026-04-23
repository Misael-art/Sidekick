using System.Text.Json;

namespace Ajudante.Nodes.Common;

internal static class JsonPathHelper
{
    public static object? ReadValue(string json, string? path)
    {
        using var document = JsonDocument.Parse(json);
        JsonElement current = document.RootElement;

        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (TryReadIndexedSegment(current, segment, out var indexedValue))
                {
                    current = indexedValue;
                    continue;
                }

                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                    throw new InvalidOperationException($"JSON path segment not found: {segment}");
            }
        }

        return ConvertElement(current);
    }

    public static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static bool TryReadIndexedSegment(JsonElement current, string segment, out JsonElement value)
    {
        value = default;
        var bracketStart = segment.IndexOf('[');
        var bracketEnd = segment.IndexOf(']');
        if (bracketStart <= 0 || bracketEnd <= bracketStart)
            return false;

        var propertyName = segment[..bracketStart];
        var indexText = segment[(bracketStart + 1)..bracketEnd];
        if (!int.TryParse(indexText, out var index))
            return false;

        if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propertyName, out var arrayElement))
            return false;
        if (arrayElement.ValueKind != JsonValueKind.Array)
            return false;

        var items = arrayElement.EnumerateArray().ToArray();
        if (index < 0 || index >= items.Length)
            return false;

        value = items[index];
        return true;
    }

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Serialize(element)
        };
    }
}
