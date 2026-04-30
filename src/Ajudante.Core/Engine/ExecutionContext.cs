using System.Collections.Concurrent;

namespace Ajudante.Core.Engine;

public static class RuntimePhases
{
    public const string Idle = "idle";
    public const string Armed = "armed";
    public const string WaitingForSchedule = "waitingForSchedule";
    public const string WaitingForWindow = "waitingForWindow";
    public const string WaitingForElement = "waitingForElement";
    public const string ElementMatched = "elementMatched";
    public const string Retrying = "retrying";
    public const string CooldownActive = "cooldownActive";
    public const string FallbackVisualActive = "fallbackVisualActive";
    public const string ClickExecuted = "clickExecuted";
    public const string Stopped = "stopped";
    public const string Error = "error";
}

public class FlowExecutionContext
{
    private readonly ConcurrentDictionary<string, object?> _variables = new();
    private readonly ConcurrentDictionary<string, Dictionary<string, object?>> _nodeOutputs = new();

    public Flow Flow { get; }
    public CancellationToken CancellationToken { get; }
    public Action<string, string, string?, object?>? PhaseSink { get; set; }
    public string CurrentNodeId { get; internal set; } = "";

    public FlowExecutionContext(Flow flow, CancellationToken ct)
    {
        Flow = flow;
        CancellationToken = ct;

        foreach (var variable in flow.Variables)
        {
            _variables[variable.Name] = variable.Default;
        }
    }

    public void SetVariable(string name, object? value)
    {
        _variables[name] = value;
    }

    public void EmitPhase(string phase, string? message = null, object? detail = null, string? nodeId = null)
    {
        try
        {
            PhaseSink?.Invoke(nodeId ?? CurrentNodeId, phase, message, detail);
        }
        catch
        {
            // Runtime phase emission is diagnostic only.
        }
    }

    public object? GetVariable(string name)
    {
        return _variables.TryGetValue(name, out var value) ? value : null;
    }

    public T? GetVariable<T>(string name)
    {
        var value = GetVariable(name);
        if (value is T typed) return typed;
        if (value is null) return default;
        try { return (T)Convert.ChangeType(value, typeof(T)); }
        catch { return default; }
    }

    public void SetNodeOutputs(string nodeId, Dictionary<string, object?> outputs)
    {
        _nodeOutputs[nodeId] = outputs;
    }

    public object? GetNodeOutput(string nodeId, string portId)
    {
        if (_nodeOutputs.TryGetValue(nodeId, out var outputs) &&
            outputs.TryGetValue(portId, out var value))
        {
            return value;
        }
        return null;
    }

    public object? ResolveReference(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return null;

        if (reference.StartsWith("var:", StringComparison.OrdinalIgnoreCase))
            return GetVariable(reference[4..]);

        var dotIndex = reference.IndexOf('.');
        if (dotIndex > 0 && dotIndex < reference.Length - 1)
        {
            var nodeId = reference[..dotIndex];
            var outputId = reference[(dotIndex + 1)..];
            var outputValue = GetNodeOutput(nodeId, outputId);
            if (outputValue is not null)
                return outputValue;
        }

        return GetVariable(reference);
    }

    public string ResolveTemplate(string template)
    {
        if (string.IsNullOrEmpty(template)) return template;

        return System.Text.RegularExpressions.Regex.Replace(
            template, @"\{\{([\w\.\-:]+)\}\}",
            match =>
            {
                var reference = match.Groups[1].Value;
                return ResolveReference(reference)?.ToString() ?? match.Value;
            });
    }
}
