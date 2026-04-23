using System.Globalization;
using System.Text.Json;
using Ajudante.Core.Engine;

namespace Ajudante.Nodes.Common;

internal static class NodeValueHelper
{
    public static string GetString(Dictionary<string, object?> properties, string key, string fallback = "")
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return fallback;

        return ConvertToString(value) ?? fallback;
    }

    public static int GetInt(Dictionary<string, object?> properties, string key, int fallback = 0)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return fallback;

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue) => intValue,
            JsonElement element when element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ when int.TryParse(ConvertToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => fallback
        };
    }

    public static bool GetBool(Dictionary<string, object?> properties, string key, bool fallback = false)
    {
        if (!properties.TryGetValue(key, out var value) || value is null)
            return fallback;

        return value switch
        {
            bool boolValue => boolValue,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            JsonElement element when element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var parsed) => parsed,
            _ when bool.TryParse(ConvertToString(value), out var parsed) => parsed,
            _ => fallback
        };
    }

    public static string ResolveTemplateProperty(FlowExecutionContext context, Dictionary<string, object?> properties, string key, string fallback = "")
    {
        return context.ResolveTemplate(GetString(properties, key, fallback));
    }

    public static void SetVariableIfRequested(FlowExecutionContext context, string variableName, object? value)
    {
        if (!string.IsNullOrWhiteSpace(variableName))
            context.SetVariable(variableName, value);
    }

    private static string? ConvertToString(object value)
    {
        return value switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.ToString(),
            _ => value.ToString()
        };
    }
}
