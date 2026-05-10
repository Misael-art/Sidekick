using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ajudante.Core.Engine;

using Flow = global::Ajudante.Core.Flow;

/// <summary>
/// Resolves <c>{{flowVariableName}}</c> in trigger property strings using <see cref="FlowVariable.Default"/>
/// values from flow variables (arm-time substitution before <see cref="ITriggerNode.StartWatchingAsync"/>).
/// Missing variables become an empty string so optional paths stay disabled.
/// </summary>
public static class FlowVariableTemplateResolver
{
    private static readonly Regex TemplateRegex = new(@"\{\{([\w\.\-:]+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Replaces <c>{{name}}</c> segments using flow variable defaults (case-insensitive variable names).
    /// </summary>
    public static string ResolveStringTemplate(string template, Flow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);
        if (string.IsNullOrEmpty(template) || template.IndexOf("{{", StringComparison.Ordinal) < 0)
            return template;

        var defaults = BuildDefaultLookup(flow);

        return TemplateRegex.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            if (!defaults.TryGetValue(name, out var raw) || raw is null)
                return string.Empty;

            return FormatDefaultForSubstitution(raw);
        });
    }

    /// <summary>
    /// Returns a new property dictionary with string values containing templates resolved for trigger activation.
    /// </summary>
    public static Dictionary<string, object?> ResolvePropertyTemplates(Flow flow, Dictionary<string, object?>? properties)
    {
        ArgumentNullException.ThrowIfNull(flow);
        if (properties is null || properties.Count == 0)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, object?>(properties, StringComparer.OrdinalIgnoreCase);
        var keys = result.Keys.ToList();
        foreach (var key in keys)
        {
            result[key] = ResolveValue(flow, result[key]);
        }

        return result;
    }

    private static object? ResolveValue(Flow flow, object? value)
    {
        var materialized = MaterializeJson(value);
        if (materialized is string s && s.IndexOf("{{", StringComparison.Ordinal) >= 0)
            return ResolveStringTemplate(s, flow);

        return materialized;
    }

    private static object? MaterializeJson(object? value)
    {
        return value switch
        {
            JsonElement element => JsonElementToObject(element),
            _ => value
        };
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static Dictionary<string, object?> BuildDefaultLookup(Flow flow)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in flow.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
                continue;
            map[variable.Name] = variable.Default;
        }

        return map;
    }

    private static string FormatDefaultForSubstitution(object value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            JsonElement je => je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? string.Empty,
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => je.ToString()
            },
            IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }
}
