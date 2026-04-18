using System.Reflection;
using System.Runtime.Loader;

namespace Ajudante.Core.Registry;

/// <summary>
/// A custom AssemblyLoadContext that loads plugin DLLs in isolation.
/// This prevents file-locking on the plugin DLL, allowing hot-swap
/// of plugins at runtime without "File in Use" errors on Windows.
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            // Load from a memory stream to avoid locking the file
            using var fs = File.OpenRead(assemblyPath);
            return LoadFromStream(fs);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
