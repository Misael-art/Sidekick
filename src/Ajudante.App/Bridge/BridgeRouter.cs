using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Ajudante.App.Overlays;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Core.Serialization;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.App.Bridge;

/// <summary>
/// Routes incoming BridgeMessages to the appropriate handler based on channel and action.
/// Manages flow persistence, engine execution, platform integration, and node registry queries.
/// </summary>
public class BridgeRouter
{
    private readonly WebBridge _bridge;
    private readonly INodeRegistry _registry;
    private readonly FlowExecutor _executor;
    private readonly string _flowsDirectory;
    private readonly Dispatcher _dispatcher;

    private Flow? _currentRunningFlow;
    private MiraWindow? _activeMiraWindow;
    private SnipWindow? _activeSnipWindow;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public event Action<string>? LogMessage;

    public BridgeRouter(
        WebBridge bridge,
        INodeRegistry registry,
        FlowExecutor executor,
        string flowsDirectory,
        Dispatcher dispatcher)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _flowsDirectory = flowsDirectory ?? throw new ArgumentNullException(nameof(flowsDirectory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        Directory.CreateDirectory(_flowsDirectory);

        // Wire executor events to push status to the frontend
        _executor.NodeStatusChanged += (nodeId, status) =>
            _ = _bridge.SendEventAsync(BridgeMessage.Channels.Engine, "nodeStatusChanged",
                new { nodeId, status = status.ToString() });

        _executor.LogMessage += (nodeId, msg) =>
            _ = _bridge.SendEventAsync(BridgeMessage.Channels.Engine, "logMessage",
                new { nodeId, message = msg });

        _executor.FlowCompleted += (flowId) =>
            _ = _bridge.SendEventAsync(BridgeMessage.Channels.Engine, "flowCompleted",
                new { flowId });

        _executor.FlowError += (flowId, error) =>
            _ = _bridge.SendEventAsync(BridgeMessage.Channels.Engine, "flowError",
                new { flowId, error });
    }

    /// <summary>
    /// Dispatches a message to the correct handler based on its channel.
    /// </summary>
    public async Task HandleMessageAsync(BridgeMessage message)
    {
        try
        {
            switch (message.Channel)
            {
                case BridgeMessage.Channels.Flow:
                    await HandleFlowChannelAsync(message);
                    break;

                case BridgeMessage.Channels.Engine:
                    await HandleEngineChannelAsync(message);
                    break;

                case BridgeMessage.Channels.Platform:
                    await HandlePlatformChannelAsync(message);
                    break;

                case BridgeMessage.Channels.Registry:
                    await HandleRegistryChannelAsync(message);
                    break;

                default:
                    await SendErrorIfRequested(message, $"Unknown channel: {message.Channel}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log($"Error handling [{message.Channel}] {message.Action}: {ex.Message}");
            await SendErrorIfRequested(message, ex.Message);
        }
    }

    // ── Flow Channel ─────────────────────────────────────────────────────

    private async Task HandleFlowChannelAsync(BridgeMessage message)
    {
        switch (message.Action)
        {
            case "saveFlow":
                await HandleSaveFlowAsync(message);
                break;

            case "loadFlow":
                await HandleLoadFlowAsync(message);
                break;

            case "listFlows":
                await HandleListFlowsAsync(message);
                break;

            case "newFlow":
                await HandleNewFlowAsync(message);
                break;

            case "deleteFlow":
                await HandleDeleteFlowAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown flow action: {message.Action}");
                break;
        }
    }

    private async Task HandleSaveFlowAsync(BridgeMessage message)
    {
        if (message.Payload == null)
        {
            await SendErrorIfRequested(message, "No flow data in payload");
            return;
        }

        var flow = JsonSerializer.Deserialize<Flow>(
            message.Payload.Value.GetRawText(), JsonOptions);

        if (flow == null)
        {
            await SendErrorIfRequested(message, "Failed to deserialize flow");
            return;
        }

        var filePath = GetFlowFilePath(flow.Id);
        await FlowSerializer.SaveAsync(flow, filePath);

        Log($"Flow saved: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { success = true, flowId = flow.Id });
        }
    }

    private async Task HandleLoadFlowAsync(BridgeMessage message)
    {
        var flowId = GetPayloadString(message.Payload, "flowId");
        if (string.IsNullOrEmpty(flowId))
        {
            await SendErrorIfRequested(message, "No flowId specified");
            return;
        }

        var filePath = GetFlowFilePath(flowId);
        var flow = await FlowSerializer.LoadAsync(filePath);

        if (flow == null)
        {
            await SendErrorIfRequested(message, $"Flow not found: {flowId}");
            return;
        }

        Log($"Flow loaded: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, flow);
        }
    }

    private async Task HandleListFlowsAsync(BridgeMessage message)
    {
        var flows = await FlowSerializer.LoadAllAsync(_flowsDirectory);

        var summaries = flows.Select(f => new
        {
            id = f.Id,
            name = f.Name,
            modifiedAt = f.ModifiedAt,
            nodeCount = f.Nodes.Count
        }).OrderByDescending(f => f.modifiedAt).ToArray();

        Log($"Listed {summaries.Length} flows");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, summaries);
        }
    }

    private async Task HandleNewFlowAsync(BridgeMessage message)
    {
        var name = GetPayloadString(message.Payload, "name") ?? "New Flow";

        var flow = new Flow
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        var filePath = GetFlowFilePath(flow.Id);
        await FlowSerializer.SaveAsync(flow, filePath);

        Log($"New flow created: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, flow);
        }
    }

    private async Task HandleDeleteFlowAsync(BridgeMessage message)
    {
        var flowId = GetPayloadString(message.Payload, "flowId");
        if (string.IsNullOrEmpty(flowId))
        {
            await SendErrorIfRequested(message, "No flowId specified");
            return;
        }

        var filePath = GetFlowFilePath(flowId);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Log($"Flow deleted: {flowId}");
        }
        else
        {
            Log($"Flow file not found for deletion: {flowId}");
        }

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { success = true });
        }
    }

    // ── Engine Channel ───────────────────────────────────────────────────

    private async Task HandleEngineChannelAsync(BridgeMessage message)
    {
        switch (message.Action)
        {
            case "runFlow":
                await HandleRunFlowAsync(message);
                break;

            case "stopFlow":
                await HandleStopFlowAsync(message);
                break;

            case "getStatus":
                await HandleGetStatusAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown engine action: {message.Action}");
                break;
        }
    }

    private async Task HandleRunFlowAsync(BridgeMessage message)
    {
        if (_executor.IsRunning)
        {
            await SendErrorIfRequested(message, "A flow is already running");
            return;
        }

        Flow? flow = null;

        // Accept either an inline flow object or a flowId reference
        if (message.Payload != null)
        {
            var payloadText = message.Payload.Value.GetRawText();
            var payloadElement = message.Payload.Value;

            if (payloadElement.ValueKind == JsonValueKind.Object &&
                payloadElement.TryGetProperty("nodes", out _))
            {
                // Inline flow object
                flow = JsonSerializer.Deserialize<Flow>(payloadText, JsonOptions);
            }
            else
            {
                // Load by flowId
                var flowId = GetPayloadString(message.Payload, "flowId");
                if (!string.IsNullOrEmpty(flowId))
                {
                    flow = await FlowSerializer.LoadAsync(GetFlowFilePath(flowId));
                }
            }
        }

        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        _currentRunningFlow = flow;

        Log($"Starting flow execution: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { started = true, flowId = flow.Id });
        }

        // Fire-and-forget the execution; events are pushed to the UI via bridge
        _ = Task.Run(async () =>
        {
            try
            {
                await _executor.ExecuteAsync(flow);
            }
            catch (Exception ex)
            {
                Log($"Flow execution failed: {ex.Message}");
                await _bridge.SendEventAsync(BridgeMessage.Channels.Engine, "flowError",
                    new { flowId = flow.Id, error = ex.Message });
            }
            finally
            {
                _currentRunningFlow = null;
            }
        });
    }

    private async Task HandleStopFlowAsync(BridgeMessage message)
    {
        if (!_executor.IsRunning)
        {
            await SendErrorIfRequested(message, "No flow is currently running");
            return;
        }

        _executor.Cancel();
        Log("Flow execution stop requested");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { stopped = true });
        }
    }

    private async Task HandleGetStatusAsync(BridgeMessage message)
    {
        var status = new
        {
            isRunning = _executor.IsRunning,
            currentFlowId = _currentRunningFlow?.Id,
            currentFlowName = _currentRunningFlow?.Name
        };

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, status);
        }
    }

    // ── Platform Channel ─────────────────────────────────────────────────

    private async Task HandlePlatformChannelAsync(BridgeMessage message)
    {
        switch (message.Action)
        {
            case "startMira":
                await HandleStartMiraAsync(message);
                break;

            case "startSnip":
                await HandleStartSnipAsync(message);
                break;

            case "cancelInspector":
                await HandleCancelInspectorAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown platform action: {message.Action}");
                break;
        }
    }

    private async Task HandleStartMiraAsync(BridgeMessage message)
    {
        Log("startMira requested");

        // Close any existing overlay first
        CloseActiveOverlays();

        await _dispatcher.InvokeAsync(() =>
        {
            var miraWindow = new MiraWindow();
            _activeMiraWindow = miraWindow;

            miraWindow.ElementCaptured += (ElementInfo element) =>
            {
                _activeMiraWindow = null;
                _ = _bridge.SendEventAsync("inspector", "elementCaptured", new
                {
                    automationId = element.AutomationId,
                    name = element.Name,
                    className = element.ClassName,
                    controlType = element.ControlType,
                    boundingRect = new
                    {
                        x = element.BoundingRect.X,
                        y = element.BoundingRect.Y,
                        width = element.BoundingRect.Width,
                        height = element.BoundingRect.Height
                    },
                    processId = element.ProcessId,
                    windowTitle = element.WindowTitle
                });
            };

            miraWindow.Closed += (s, e) => _activeMiraWindow = null;
            miraWindow.Show();
        });

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { started = true });
        }
    }

    private async Task HandleStartSnipAsync(BridgeMessage message)
    {
        Log("startSnip requested");

        // Close any existing overlay first
        CloseActiveOverlays();

        await _dispatcher.InvokeAsync(() =>
        {
            var snipWindow = new SnipWindow();
            _activeSnipWindow = snipWindow;

            snipWindow.RegionCaptured += (byte[] pngBytes, System.Drawing.Rectangle bounds) =>
            {
                _activeSnipWindow = null;
                var base64 = Convert.ToBase64String(pngBytes);
                _ = _bridge.SendEventAsync("inspector", "regionCaptured", new
                {
                    image = base64,
                    bounds = new
                    {
                        x = bounds.X,
                        y = bounds.Y,
                        width = bounds.Width,
                        height = bounds.Height
                    }
                });
            };

            snipWindow.Closed += (s, e) => _activeSnipWindow = null;
            snipWindow.Show();
        });

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { started = true });
        }
    }

    private async Task HandleCancelInspectorAsync(BridgeMessage message)
    {
        Log("cancelInspector requested");

        CloseActiveOverlays();

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { cancelled = true });
        }
    }

    private void CloseActiveOverlays()
    {
        _dispatcher.Invoke(() =>
        {
            if (_activeMiraWindow != null)
            {
                _activeMiraWindow.Close();
                _activeMiraWindow = null;
            }

            if (_activeSnipWindow != null)
            {
                _activeSnipWindow.Close();
                _activeSnipWindow = null;
            }
        });
    }

    // ── Registry Channel ─────────────────────────────────────────────────

    private async Task HandleRegistryChannelAsync(BridgeMessage message)
    {
        switch (message.Action)
        {
            case "getNodeDefinitions":
                await HandleGetNodeDefinitionsAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown registry action: {message.Action}");
                break;
        }
    }

    private async Task HandleGetNodeDefinitionsAsync(BridgeMessage message)
    {
        var definitions = _registry.GetAllDefinitions();
        Log($"Returning {definitions.Length} node definitions");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, definitions);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private string GetFlowFilePath(string flowId)
    {
        // Sanitize the flow ID to prevent path traversal
        var safeId = Path.GetFileNameWithoutExtension(flowId);
        return Path.Combine(_flowsDirectory, $"{safeId}.json");
    }

    private static string? GetPayloadString(JsonElement? payload, string propertyName)
    {
        if (payload == null) return null;

        // If the payload is a string, treat it as the value directly
        if (payload.Value.ValueKind == JsonValueKind.String)
            return payload.Value.GetString();

        // Otherwise look for a named property
        if (payload.Value.ValueKind == JsonValueKind.Object &&
            payload.Value.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private async Task SendErrorIfRequested(BridgeMessage message, string error)
    {
        if (message.RequestId != null)
        {
            await _bridge.SendErrorResponseAsync(message.RequestId, error);
        }
    }

    private void Log(string message)
    {
        LogMessage?.Invoke($"[BridgeRouter] {message}");
    }
}
