using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Ajudante.App.Assets;
using Ajudante.App.Overlays;
using Ajudante.App.Runtime;
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
    private readonly FlowRuntimeManager _runtimeManager;
    private readonly string _flowsDirectory;
    private readonly Dispatcher _dispatcher;
    private readonly SnipAssetCatalog _snipAssetCatalog;
    private readonly MiraInspectionCatalog _miraInspectionCatalog;
    private readonly HashSet<string> _nativeBundledFlowIds;

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
        FlowRuntimeManager runtimeManager,
        string flowsDirectory,
        Dispatcher dispatcher,
        SnipAssetCatalog snipAssetCatalog,
        MiraInspectionCatalog miraInspectionCatalog)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        _flowsDirectory = flowsDirectory ?? throw new ArgumentNullException(nameof(flowsDirectory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _snipAssetCatalog = snipAssetCatalog ?? throw new ArgumentNullException(nameof(snipAssetCatalog));
        _miraInspectionCatalog = miraInspectionCatalog ?? throw new ArgumentNullException(nameof(miraInspectionCatalog));
        _nativeBundledFlowIds = LoadNativeBundledFlowIds();

        Directory.CreateDirectory(_flowsDirectory);

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

                case BridgeMessage.Channels.Assets:
                    await HandleAssetsChannelAsync(message);
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

            case "exportRunnerPackage":
                await HandleExportRunnerPackageAsync(message);
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

        if (string.IsNullOrWhiteSpace(flow.Name))
        {
            flow.Name = "Untitled Flow";
        }

        var matchingFilePaths = await FindFlowFilePathsAsync(flow.Id);
        var filePath = matchingFilePaths.FirstOrDefault() ?? GetFlowFilePath(flow.Id);
        await FlowSerializer.SaveAsync(flow, filePath);
        DeleteDuplicateFlowFiles(matchingFilePaths, filePath);

        Log($"Flow saved: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { success = true, id = flow.Id, flowId = flow.Id });
        }
    }

    private async Task HandleLoadFlowAsync(BridgeMessage message)
    {
        var flowId = GetPayloadString(message.Payload, "flowId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrEmpty(flowId))
        {
            await SendErrorIfRequested(message, "No flowId specified");
            return;
        }

        var filePath = await FindFlowFilePathAsync(flowId);
        var flow = await FlowSerializer.LoadAsync(filePath);

        if (flow == null)
        {
            await SendErrorIfRequested(message, $"Flow not found: {flowId}");
            return;
        }

        TryEnrichVariablesFromBundledSeed(flow);

        Log($"Flow loaded: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, flow);
        }
    }

    private async Task HandleListFlowsAsync(BridgeMessage message)
    {
        var flows = await FlowSerializer.LoadAllAsync(_flowsDirectory);

        var summaries = flows
        .Where(f => !string.IsNullOrWhiteSpace(f.Id))
        .GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase)
        .Select(group => group
            .OrderByDescending(f => f.ModifiedAt)
            .First())
        .Select(f => new
        {
            id = f.Id,
            name = f.Name,
            modifiedAt = f.ModifiedAt,
            nodeCount = f.Nodes.Count,
            isNative = IsNativeFlowId(f.Id)
        }).OrderByDescending(f => f.modifiedAt).ToArray();

        Log($"Listed {summaries.Length} flows");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, summaries);
        }
    }

    private async Task HandleNewFlowAsync(BridgeMessage message)
    {
        var name = GetPayloadString(message.Payload, "name") ?? "Untitled Flow";

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
        var flowId = GetPayloadString(message.Payload, "flowId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrEmpty(flowId))
        {
            await SendErrorIfRequested(message, "No flowId specified");
            return;
        }

        if (IsNativeFlowId(flowId))
        {
            await SendErrorIfRequested(message, "Native bundled flows cannot be deleted.");
            return;
        }

        var deletedPaths = DeleteFlowFiles(await FindFlowFilePathsAsync(flowId));
        if (deletedPaths > 0)
        {
            Log($"Flow deleted: {flowId} ({deletedPaths} file(s))");
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

    private async Task HandleExportRunnerPackageAsync(BridgeMessage message)
    {
        if (message.Payload == null)
        {
            await SendErrorIfRequested(message, "No flow data in payload");
            return;
        }

        var flow = JsonSerializer.Deserialize<Flow>(message.Payload.Value.GetRawText(), JsonOptions);
        if (flow == null)
        {
            await SendErrorIfRequested(message, "Failed to deserialize flow");
            return;
        }

        var safeName = string.Concat((flow.Name ?? "flow").Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "flow";
        }

        var exportRoot = Path.Combine(Ajudante.App.App.DataDirectory, "exports");
        var packageDirectory = Path.Combine(exportRoot, $"{safeName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(packageDirectory);

        var flowPath = Path.Combine(packageDirectory, "flow.json");
        await FlowSerializer.SaveAsync(flow, flowPath);

        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var copiedFiles = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(baseDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(sourcePath);
            if (fileName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(sourcePath, Path.Combine(packageDirectory, fileName), overwrite: true);
            copiedFiles++;
        }

        var wwwroot = Path.Combine(baseDirectory, "wwwroot");
        if (Directory.Exists(wwwroot))
        {
            CopyDirectory(wwwroot, Path.Combine(packageDirectory, "wwwroot"));
        }

        var commandPath = Path.Combine(packageDirectory, "run-sidekick-flow.cmd");
        await File.WriteAllTextAsync(commandPath,
            "@echo off\r\n" +
            "set DIR=%~dp0\r\n" +
            "\"%DIR%Sidekick.exe\" --run-flow \"%DIR%flow.json\" --exit-after-run\r\n");

        await File.WriteAllTextAsync(Path.Combine(packageDirectory, "README.txt"),
            "Sidekick Export Runner\r\n\r\n" +
            "Execute run-sidekick-flow.cmd para rodar este flow fora do editor visual.\r\n" +
            "Limite honesto: se uma instancia do Sidekick ja estiver aberta, o mutex de instancia unica pode bloquear a execucao autonomoma.\r\n" +
            "Logs ficam em %AppData%\\Sidekick\\logs.\r\n");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                success = true,
                packageDirectory,
                flowPath,
                commandPath,
                copiedFiles
            });
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

            case "validateFlow":
                await HandleValidateFlowAsync(message);
                break;

            case "stopFlow":
                await HandleStopFlowAsync(message);
                break;

            case "activateFlow":
                await HandleActivateFlowAsync(message);
                break;

            case "deactivateFlow":
                await HandleDeactivateFlowAsync(message);
                break;

            case "getStatus":
                await HandleGetStatusAsync(message);
                break;

            case "getRuntimeStatus":
                await HandleGetRuntimeStatusAsync(message);
                break;

            case "getExecutionHistory":
                await HandleGetExecutionHistoryAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown engine action: {message.Action}");
                break;
        }
    }

    private async Task HandleRunFlowAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);

        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var validation = ValidateFlow(flow);
        if (!validation.IsValid)
        {
            Log($"Flow run blocked by validation: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    queued = false,
                    flowId = flow.Id,
                    validation
                });
            }

            return;
        }

        var queueEvent = _runtimeManager.QueueManualRun(flow);
        Log($"Flow queued for execution: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                queued = true,
                flowId = flow.Id,
                queueLength = queueEvent.QueueLength,
                queuePending = queueEvent.QueuePending,
                validation
            });
        }
    }

    private async Task HandleValidateFlowAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);

        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var validation = ValidateFlow(flow);
        Log($"Flow validated: {flow.Name} ({flow.Id}) - valid={validation.IsValid}");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, validation);
        }
    }

    private async Task HandleStopFlowAsync(BridgeMessage message)
    {
        var status = _runtimeManager.GetRuntimeStatus();
        if (!status.IsRunning && status.QueueLength == 0)
        {
            await SendErrorIfRequested(message, "No flow is currently running or queued");
            return;
        }

        var mode = GetStopFlowMode(message.Payload);
        var result = _runtimeManager.Stop(mode);
        Log($"Runtime stop requested ({mode})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
        }
    }

    private async Task HandleGetStatusAsync(BridgeMessage message)
    {
        var status = _runtimeManager.GetRuntimeStatus();
        var currentRun = status.CurrentRun;
        var payload = new
        {
            isRunning = status.IsRunning,
            currentFlowId = currentRun?.FlowId,
            currentFlowName = currentRun?.FlowName,
            queueLength = status.QueueLength,
            armedFlowCount = status.ArmedFlowCount,
            currentRun,
            flows = status.Flows
        };

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, payload);
        }
    }

    private async Task HandleGetRuntimeStatusAsync(BridgeMessage message)
    {
        var status = _runtimeManager.GetRuntimeStatus();
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, status);
        }
    }

    private async Task HandleGetExecutionHistoryAsync(BridgeMessage message)
    {
        var request = DeserializePayload<ExecutionHistoryRequest>(message.Payload) ?? new ExecutionHistoryRequest();
        var history = await _runtimeManager.GetExecutionHistoryAsync(request.Limit, request.FlowId);

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, history);
        }
    }

    private async Task HandleActivateFlowAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);
        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var validation = ValidateFlow(flow);
        if (!validation.IsValid)
        {
            Log($"Flow activation blocked by validation: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    armed = false,
                    flow = (object?)null,
                    warnings = validation.Warnings,
                    validation
                });
            }

            return;
        }

        var activation = await _runtimeManager.ActivateFlowAsync(flow);
        Log($"Flow armed for continuous runtime: {flow.Name} ({flow.Id})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                armed = activation.Armed,
                flow = activation.Snapshot,
                warnings = activation.Warnings,
                validation
            });
        }
    }

    private async Task HandleDeactivateFlowAsync(BridgeMessage message)
    {
        var flowId = GetPayloadString(message.Payload, "flowId");
        if (string.IsNullOrWhiteSpace(flowId))
        {
            await SendErrorIfRequested(message, "No flowId specified");
            return;
        }

        var snapshot = await _runtimeManager.DeactivateFlowAsync(flowId);
        Log($"Flow disarmed: {flowId}");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                disarmed = snapshot != null,
                flow = snapshot
            });
        }
    }

    // ── Platform Channel ─────────────────────────────────────────────────

    private async Task<Flow?> ResolveFlowAsync(JsonElement? payload)
    {
        if (payload == null)
        {
            return null;
        }

        var payloadElement = payload.Value;
        if (payloadElement.ValueKind == JsonValueKind.Object &&
            payloadElement.TryGetProperty("nodes", out _))
        {
            var flow = JsonSerializer.Deserialize<Flow>(payloadElement.GetRawText(), JsonOptions);
            if (flow != null)
            {
                TryEnrichVariablesFromBundledSeed(flow);
            }

            return flow;
        }

        var flowId = GetPayloadString(payload, "flowId");
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return null;
        }

        var diskFlow = await FlowSerializer.LoadAsync(await FindFlowFilePathAsync(flowId));
        if (diskFlow != null)
        {
            TryEnrichVariablesFromBundledSeed(diskFlow);
        }

        return diskFlow;
    }

    private static StopFlowMode GetStopFlowMode(JsonElement? payload)
    {
        var rawMode = GetPayloadString(payload, "mode");
        return rawMode?.Trim().ToLowerInvariant() switch
        {
            "cancelall" => StopFlowMode.CancelAll,
            _ => StopFlowMode.CurrentOnly
        };
    }

    private ValidationResult ValidateFlow(Flow flow)
    {
        var validator = new FlowValidator(_registry);
        return validator.Validate(flow);
    }

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

    private async Task HandleAssetsChannelAsync(BridgeMessage message)
    {
        switch (message.Action)
        {
            case "listSnipAssets":
                await HandleListSnipAssetsAsync(message);
                break;

            case "getSnipAssetTemplate":
                await HandleGetSnipAssetTemplateAsync(message);
                break;

            case "listInspectionAssets":
                await HandleListInspectionAssetsAsync(message);
                break;

            case "deleteInspectionAsset":
                await HandleDeleteInspectionAssetAsync(message);
                break;

            case "updateInspectionAsset":
                await HandleUpdateInspectionAssetAsync(message);
                break;

            case "duplicateInspectionAsset":
                await HandleDuplicateInspectionAssetAsync(message);
                break;

            case "testInspectionAsset":
                await HandleTestInspectionAssetAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown assets action: {message.Action}");
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
                _ = Task.Run(async () =>
                {
                    MiraInspectionManifest? asset = null;
                    string? assetSaveError = null;

                    try
                    {
                        asset = await _miraInspectionCatalog.SaveCaptureAsync(element);
                        await _bridge.SendEventAsync(BridgeMessage.Channels.Assets, "inspectionAssetSaved", asset);
                    }
                    catch (Exception ex)
                    {
                        assetSaveError = ex.Message;
                        Log($"Failed to persist Mira inspection asset: {ex.Message}");
                    }

                    await _bridge.SendEventAsync("inspector", "elementCaptured", new
                    {
                        automationId = element.AutomationId,
                        name = element.Name,
                        valueText = element.ValueText,
                        textPatternText = element.TextPatternText,
                        legacyName = element.LegacyName,
                        legacyValue = element.LegacyValue,
                        helpText = element.HelpText,
                        detectedText = element.DetectedText,
                        currentText = element.CurrentText,
                        placeholderText = element.PlaceholderText,
                        textSource = element.TextSource,
                        captureQuality = element.CaptureQuality,
                        ocrAttempted = element.OcrAttempted,
                        ocrAvailable = element.OcrAvailable,
                        ocrText = element.OcrText,
                        ocrWarning = element.OcrWarning,
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
                        processName = element.ProcessName,
                        processPath = element.ProcessPath,
                        windowTitle = element.WindowTitle,
                        windowBounds = new
                        {
                            x = element.WindowBounds.X,
                            y = element.WindowBounds.Y,
                            width = element.WindowBounds.Width,
                            height = element.WindowBounds.Height
                        },
                        relativeBoundingRect = new
                        {
                            x = element.RelativeBoundingRect.X,
                            y = element.RelativeBoundingRect.Y,
                            width = element.RelativeBoundingRect.Width,
                            height = element.RelativeBoundingRect.Height
                        },
                        cursorScreen = new
                        {
                            x = element.CursorScreen.X,
                            y = element.CursorScreen.Y
                        },
                        cursorPixelColor = element.CursorPixelColor,
                        isFocused = element.IsFocused,
                        isEnabled = element.IsEnabled,
                        isVisible = !element.IsOffscreen,
                        windowStateAtCapture = element.WindowStateAtCapture,
                        windowHandle = element.WindowHandle == 0 ? (long?)null : element.WindowHandle,
                        monitorDeviceName = element.MonitorDeviceName,
                        monitorBounds = new
                        {
                            x = element.MonitorBounds.X,
                            y = element.MonitorBounds.Y,
                            width = element.MonitorBounds.Width,
                            height = element.MonitorBounds.Height
                        },
                        hostScreenWidth = element.HostScreenWidth,
                        hostScreenHeight = element.HostScreenHeight,
                        dpiScale = element.DpiScale,
                        relativePointX = element.RelativePointX,
                        relativePointY = element.RelativePointY,
                        normalizedWindowX = element.NormalizedWindowX,
                        normalizedWindowY = element.NormalizedWindowY,
                        normalizedScreenX = element.NormalizedScreenX,
                        normalizedScreenY = element.NormalizedScreenY,
                        selectorStrength = SelectorStrengthEvaluator.ToPublicLabel(SelectorStrengthEvaluator.Evaluate(element)),
                        selectorStrategy = SelectorStrengthEvaluator.ToPublicStrategy(SelectorStrengthEvaluator.SuggestStrategy(element)),
                        asset,
                        assetSaveError
                    });
                });
            };

            miraWindow.Closed += (s, e) => _activeMiraWindow = null;
            miraWindow.Show();
            miraWindow.Activate();
            miraWindow.Focus();
        });

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { started = true });
        }
    }

    private async Task HandleGetSnipAssetTemplateAsync(BridgeMessage message)
    {
        var assetId = GetPayloadString(message.Payload, "assetId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            await SendErrorIfRequested(message, "No assetId specified");
            return;
        }

        var asset = await _snipAssetCatalog.GetAsync(assetId);
        if (asset == null)
        {
            await SendErrorIfRequested(message, $"Snip asset not found: {assetId}");
            return;
        }

        var imageBase64 = await _snipAssetCatalog.GetImageBase64Async(assetId);
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            await SendErrorIfRequested(message, $"Snip asset image not found: {assetId}");
            return;
        }

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                assetId = asset.Id,
                displayName = asset.DisplayName,
                imagePath = asset.Content.ImagePath,
                imageBase64
            });
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

            snipWindow.RegionCaptured += async (byte[] pngBytes, System.Drawing.Rectangle bounds) =>
            {
                _activeSnipWindow = null;
                var base64 = Convert.ToBase64String(pngBytes);
                SnipAssetManifest? asset = null;
                string? assetSaveError = null;

                try
                {
                    asset = await _snipAssetCatalog.SaveCaptureAsync(pngBytes, bounds);
                    await _bridge.SendEventAsync(BridgeMessage.Channels.Assets, "snipAssetSaved", asset);
                }
                catch (Exception ex)
                {
                    assetSaveError = ex.Message;
                    Log($"Failed to persist snip asset: {ex.Message}");
                }

                await _bridge.SendEventAsync("inspector", "regionCaptured", new
                {
                    image = base64,
                    bounds = new
                    {
                        x = bounds.X,
                        y = bounds.Y,
                        width = bounds.Width,
                        height = bounds.Height
                    },
                    asset,
                    assetSaveError
                });
            };

            snipWindow.Closed += (s, e) => _activeSnipWindow = null;
            snipWindow.Show();
            snipWindow.Activate();
            snipWindow.Focus();
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

    private async Task HandleListSnipAssetsAsync(BridgeMessage message)
    {
        var assets = await _snipAssetCatalog.ListAsync();
        Log($"Listed {assets.Count} snip assets");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, assets);
        }
    }

    private async Task HandleListInspectionAssetsAsync(BridgeMessage message)
    {
        var assets = await _miraInspectionCatalog.ListAsync();
        Log($"Listed {assets.Count} inspection assets");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, assets);
        }
    }

    private async Task HandleDeleteInspectionAssetAsync(BridgeMessage message)
    {
        var assetId = GetPayloadString(message.Payload, "assetId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            await SendErrorIfRequested(message, "No inspection asset id specified");
            return;
        }

        var deleted = await _miraInspectionCatalog.DeleteAsync(assetId);
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new { success = deleted });
        }
    }

    private async Task HandleUpdateInspectionAssetAsync(BridgeMessage message)
    {
        var assetId = GetPayloadString(message.Payload, "assetId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            await SendErrorIfRequested(message, "No inspection asset id specified");
            return;
        }

        var displayName = GetPayloadString(message.Payload, "displayName");
        var notes = GetPayloadString(message.Payload, "notes");
        var tags = GetPayloadStringArray(message.Payload, "tags");
        var asset = await _miraInspectionCatalog.UpdateAsync(assetId, displayName, notes, tags);
        if (asset is null)
        {
            await SendErrorIfRequested(message, $"Inspection asset not found: {assetId}");
            return;
        }

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, asset);
        }
    }

    private async Task HandleDuplicateInspectionAssetAsync(BridgeMessage message)
    {
        var assetId = GetPayloadString(message.Payload, "assetId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            await SendErrorIfRequested(message, "No inspection asset id specified");
            return;
        }

        var displayName = GetPayloadString(message.Payload, "displayName");
        var asset = await _miraInspectionCatalog.DuplicateAsync(assetId, displayName);
        if (asset is null)
        {
            await SendErrorIfRequested(message, $"Inspection asset not found: {assetId}");
            return;
        }

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, asset);
        }
    }

    private async Task HandleTestInspectionAssetAsync(BridgeMessage message)
    {
        var assetId = GetPayloadString(message.Payload, "assetId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            await SendErrorIfRequested(message, "No inspection asset id specified");
            return;
        }

        var asset = await _miraInspectionCatalog.GetAsync(assetId);
        if (asset == null)
        {
            await SendErrorIfRequested(message, $"Inspection asset not found: {assetId}");
            return;
        }

        var selector = asset.Locator.Selector;
        var found = AutomationElementLocator.FindElement(
            selector.WindowTitle ?? asset.Source.WindowTitle,
            selector.AutomationId,
            selector.Name,
            selector.ControlType,
            1000,
            asset.Source.ProcessName,
            asset.Source.ProcessPath,
            AutomationElementLocator.TitleMatch.Contains);

        var rect = found?.Current.BoundingRectangle;
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                found = found != null,
                bounds = found == null || rect == null
                    ? null
                    : new
                    {
                        x = (int)rect.Value.X,
                        y = (int)rect.Value.Y,
                        width = (int)rect.Value.Width,
                        height = (int)rect.Value.Height
                    },
                strategy = asset.Locator.Strategy,
                strength = asset.Locator.Strength
            });
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

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);
        foreach (var filePath in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(filePath, Path.Combine(destinationDirectory, Path.GetFileName(filePath)), overwrite: true);
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            CopyDirectory(directoryPath, Path.Combine(destinationDirectory, Path.GetFileName(directoryPath)));
        }
    }

    private async Task<string> FindFlowFilePathAsync(string flowId)
    {
        var matchingPaths = await FindFlowFilePathsAsync(flowId);
        if (matchingPaths.Count > 0)
        {
            return matchingPaths[0];
        }

        return GetFlowFilePath(flowId);
    }

    private async Task<List<string>> FindFlowFilePathsAsync(string flowId)
    {
        var matches = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddPath(string path)
        {
            var normalizedPath = Path.GetFullPath(path);
            if (seenPaths.Add(normalizedPath))
            {
                matches.Add(normalizedPath);
            }
        }

        var exactPath = GetFlowFilePath(flowId);
        if (File.Exists(exactPath))
        {
            AddPath(exactPath);
        }

        foreach (var candidatePath in Directory.EnumerateFiles(_flowsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var flow = await FlowSerializer.LoadAsync(candidatePath);
            if (flow?.Id != null && string.Equals(flow.Id, flowId, StringComparison.OrdinalIgnoreCase))
            {
                AddPath(candidatePath);
            }
        }

        return matches;
    }

    private static void DeleteDuplicateFlowFiles(IEnumerable<string> candidatePaths, string keptPath)
    {
        var normalizedKeptPath = Path.GetFullPath(keptPath);

        foreach (var candidatePath in candidatePaths)
        {
            if (string.Equals(candidatePath, normalizedKeptPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(candidatePath);
            }
            catch
            {
                // Keep save successful even if best-effort duplicate cleanup fails.
            }
        }
    }

    private static int DeleteFlowFiles(IEnumerable<string> candidatePaths)
    {
        var deletedCount = 0;

        foreach (var candidatePath in candidatePaths)
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            File.Delete(candidatePath);
            deletedCount++;
        }

        return deletedCount;
    }

    private bool IsNativeFlowId(string? flowId)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return false;
        }

        return _nativeBundledFlowIds.Contains(flowId);
    }

    /// <summary>
    /// Copies <see cref="Flow.Variables"/> from the bundled seed JSON when the user copy on disk
    /// is stale (e.g. seeded before variables existed). Seeding uses overwrite:false, so AppData
    /// files may lack <c>variables</c> while the editor still references <c>{{name}}</c> templates.
    /// </summary>
    private void TryEnrichVariablesFromBundledSeed(Flow flow)
    {
        if (flow.Variables.Count > 0 || string.IsNullOrWhiteSpace(flow.Id) || !IsNativeFlowId(flow.Id))
        {
            return;
        }

        var bundledFlowsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed-flows");
        if (!Directory.Exists(bundledFlowsDirectory))
        {
            return;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(bundledFlowsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var seed = FlowSerializer.Deserialize(File.ReadAllText(sourcePath));
                if (seed == null || !string.Equals(seed.Id, flow.Id, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seed.Variables.Count == 0)
                {
                    return;
                }

                flow.Variables = seed.Variables
                    .Select(v => new FlowVariable { Name = v.Name, Type = v.Type, Default = v.Default })
                    .ToList();
                Log($"Enriched {flow.Variables.Count} flow variable(s) from bundled seed for native flow '{flow.Id}'.");
                return;
            }
            catch (Exception ex)
            {
                Log($"Bundled seed parse skipped for '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }
    }

    private static HashSet<string> LoadNativeBundledFlowIds()
    {
        var flowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var bundledFlowsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed-flows");
        if (!Directory.Exists(bundledFlowsDirectory))
        {
            return flowIds;
        }

        foreach (var sourcePath in Directory.EnumerateFiles(bundledFlowsDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var flow = FlowSerializer.Deserialize(File.ReadAllText(sourcePath));
                if (!string.IsNullOrWhiteSpace(flow?.Id))
                {
                    flowIds.Add(flow.Id);
                }
            }
            catch
            {
                // Keep startup resilient even if a bundled sample is malformed.
            }
        }

        return flowIds;
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

    private static string[]? GetPayloadStringArray(JsonElement? payload, string propertyName)
    {
        if (payload == null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!payload.Value.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return prop
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();
    }

    private static TPayload? DeserializePayload<TPayload>(JsonElement? payload)
    {
        if (payload == null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<TPayload>(payload.Value.GetRawText(), JsonOptions);
    }

    private sealed class ExecutionHistoryRequest
    {
        public int Limit { get; set; } = 50;
        public string? FlowId { get; set; }
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
