using System.Reflection;

namespace Ajudante.Core.Interfaces;

public interface INodeRegistry
{
    void ScanAssembly(Assembly assembly);
    void ScanDirectory(string pluginPath);
    NodeDefinition[] GetAllDefinitions();
    INode CreateInstance(string typeId);
    NodeDefinition? GetDefinition(string typeId);
}
