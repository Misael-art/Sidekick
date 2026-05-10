using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ajudante.Core.Serialization;

public static class FlowSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(Flow flow)
    {
        flow.ModifiedAt = DateTime.UtcNow;
        return JsonSerializer.Serialize(flow, Options);
    }

    public static Flow? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<Flow>(json, Options);
    }

    public static async Task SaveAsync(Flow flow, string filePath)
    {
        var json = Serialize(flow);
        var directory = Path.GetDirectoryName(filePath);
        if (directory != null && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(filePath, json);
    }

    public static async Task<Flow?> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;
        var json = await File.ReadAllTextAsync(filePath);
        return Deserialize(json);
    }

    public static async Task<List<Flow>> LoadAllAsync(string directoryPath)
    {
        var flows = new List<Flow>();
        if (!Directory.Exists(directoryPath)) return flows;

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                var flow = await LoadAsync(file);
                if (flow != null) flows.Add(flow);
            }
            catch (JsonException)
            {
                // A directory may contain non-flow JSON sidecars such as recipe catalogs.
            }
            catch (NotSupportedException)
            {
                // Keep flow listing resilient when a JSON file is not shaped like a Flow.
            }
        }

        return flows;
    }
}
