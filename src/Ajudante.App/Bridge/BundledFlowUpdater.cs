using System.Text.Json;
using System.Text.Json.Serialization;
using Ajudante.Core;

namespace Ajudante.App.Bridge;

public static class BundledFlowUpdater
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static bool RefreshFromSeedIfNewer(Flow target, Flow seed)
    {
        if (!IsSameFlow(target, seed) || seed.Version <= target.Version)
            return false;

        var preservedVariables = target.Variables
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
            .GroupBy(variable => variable.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var clone = Clone(seed);

        target.Name = clone.Name;
        target.Version = clone.Version;
        target.Nodes = clone.Nodes;
        target.Connections = clone.Connections;
        target.Annotations = clone.Annotations;
        target.Variables = MergeSeedVariables(clone.Variables, preservedVariables);
        target.ModifiedAt = DateTime.UtcNow;

        return true;
    }

    public static int MergeMissingVariables(Flow target, Flow seed)
    {
        if (!IsSameFlow(target, seed) || seed.Variables.Count == 0)
            return 0;

        var existingNames = target.Variables
            .Select(variable => variable.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var variable in seed.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name) || !existingNames.Add(variable.Name))
                continue;

            target.Variables.Add(CloneVariable(variable));
            added++;
        }

        return added;
    }

    private static bool IsSameFlow(Flow target, Flow seed)
    {
        return !string.IsNullOrWhiteSpace(target.Id) &&
               !string.IsNullOrWhiteSpace(seed.Id) &&
               string.Equals(target.Id, seed.Id, StringComparison.OrdinalIgnoreCase);
    }

    private static List<FlowVariable> MergeSeedVariables(
        IEnumerable<FlowVariable> seedVariables,
        IReadOnlyDictionary<string, FlowVariable> preservedVariables)
    {
        var merged = new List<FlowVariable>();
        var seedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var seedVariable in seedVariables)
        {
            if (string.IsNullOrWhiteSpace(seedVariable.Name) || !seedNames.Add(seedVariable.Name))
                continue;

            if (preservedVariables.TryGetValue(seedVariable.Name, out var preserved))
            {
                merged.Add(new FlowVariable
                {
                    Name = seedVariable.Name,
                    Type = seedVariable.Type,
                    Default = preserved.Default
                });
                continue;
            }

            merged.Add(CloneVariable(seedVariable));
        }

        foreach (var preserved in preservedVariables.Values)
        {
            if (seedNames.Contains(preserved.Name))
                continue;

            merged.Add(CloneVariable(preserved));
        }

        return merged;
    }

    private static FlowVariable CloneVariable(FlowVariable source)
    {
        return new FlowVariable
        {
            Name = source.Name,
            Type = source.Type,
            Default = CloneObject(source.Default)
        };
    }

    private static Flow Clone(Flow source)
    {
        return JsonSerializer.Deserialize<Flow>(
            JsonSerializer.Serialize(source, JsonOptions),
            JsonOptions) ?? new Flow();
    }

    private static object? CloneObject(object? value)
    {
        if (value is null)
            return null;

        return JsonSerializer.Deserialize<object>(
            JsonSerializer.Serialize(value, JsonOptions),
            JsonOptions);
    }
}
