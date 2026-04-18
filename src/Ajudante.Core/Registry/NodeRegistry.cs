using System.Reflection;
using Ajudante.Core.Interfaces;

namespace Ajudante.Core.Registry;

public class NodeRegistry : INodeRegistry
{
    private readonly Dictionary<string, NodeRegistryEntry> _entries = new();

    public void ScanAssembly(Assembly assembly)
    {
        var nodeTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(INode).IsAssignableFrom(t))
            .Where(t => t.GetCustomAttribute<NodeInfoAttribute>() != null);

        foreach (var type in nodeTypes)
        {
            var attr = type.GetCustomAttribute<NodeInfoAttribute>()!;
            var definition = BuildDefinition(type, attr);

            _entries[attr.TypeId] = new NodeRegistryEntry
            {
                Type = type,
                Definition = definition
            };
        }
    }

    /// <summary>
    /// Scans a directory for plugin DLLs, loading each into an isolated
    /// AssemblyLoadContext to avoid file-locking on Windows.
    /// This allows plugins to be updated or replaced at runtime.
    /// </summary>
    public void ScanDirectory(string pluginPath)
    {
        if (!Directory.Exists(pluginPath)) return;

        foreach (var dll in Directory.GetFiles(pluginPath, "*.dll"))
        {
            try
            {
                // Use an isolated load context to prevent locking the DLL file
                var loadContext = new PluginLoadContext(dll);

                // Read the DLL into memory so the file handle is released immediately
                using var fs = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.Read);
                var assembly = loadContext.LoadFromStream(fs);
                ScanAssembly(assembly);
            }
            catch
            {
                // Skip invalid assemblies
            }
        }
    }

    public NodeDefinition[] GetAllDefinitions()
    {
        return _entries.Values.Select(e => e.Definition).ToArray();
    }

    public INode CreateInstance(string typeId)
    {
        if (!_entries.TryGetValue(typeId, out var entry))
            throw new InvalidOperationException($"Unknown node type: {typeId}");

        return (INode)Activator.CreateInstance(entry.Type)!;
    }

    public NodeDefinition? GetDefinition(string typeId)
    {
        return _entries.TryGetValue(typeId, out var entry) ? entry.Definition : null;
    }

    private static NodeDefinition BuildDefinition(Type type, NodeInfoAttribute attr)
    {
        var instance = (INode)Activator.CreateInstance(type)!;
        var def = instance.Definition;

        return new NodeDefinition
        {
            TypeId = attr.TypeId,
            DisplayName = attr.DisplayName,
            Category = attr.Category,
            Description = attr.Description,
            Color = attr.Color.Length > 0 ? attr.Color : GetDefaultColor(attr.Category),
            InputPorts = def.InputPorts,
            OutputPorts = def.OutputPorts,
            Properties = def.Properties
        };
    }

    private static string GetDefaultColor(NodeCategory category) => category switch
    {
        NodeCategory.Trigger => "#EF4444",
        NodeCategory.Logic => "#EAB308",
        NodeCategory.Action => "#22C55E",
        _ => "#888888"
    };

    private class NodeRegistryEntry
    {
        public required Type Type { get; init; }
        public required NodeDefinition Definition { get; init; }
    }
}
