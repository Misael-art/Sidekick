using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Threading;
using Ajudante.App.Assets;
using Ajudante.App.Configuration;
using Ajudante.App.Overlays;
using Ajudante.App.Runtime;
using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Core.Recorder;
using Ajudante.Core.Serialization;
using Ajudante.Platform.Input;
using Ajudante.Platform.UIAutomation;

namespace Ajudante.App.Bridge;

/// <summary>
/// Routes incoming BridgeMessages to the appropriate handler based on channel and action.
/// Manages flow persistence, engine execution, platform integration, and node registry queries.
/// </summary>
public class BridgeRouter : IDisposable
{
    private readonly WebBridge _bridge;
    private readonly INodeRegistry _registry;
    private readonly FlowRuntimeManager _runtimeManager;
    private readonly string _flowsDirectory;
    private readonly Dispatcher _dispatcher;
    private readonly SnipAssetCatalog _snipAssetCatalog;
    private readonly MiraInspectionCatalog _miraInspectionCatalog;
    private readonly HashSet<string> _nativeBundledFlowIds;
    private readonly string _dataDirectory;
    private readonly SecuritySettingsStore _securitySettingsStore;
    private readonly string _securityAuditLogPath;
    private readonly FlowInvocationService _flowInvocationService;

    private MiraWindow? _activeMiraWindow;
    private SnipWindow? _activeSnipWindow;
    private readonly MacroRecorderService _macroRecorderService = new();
    private GuidedAutomationDraft? _lastMacroDraft;

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
        string dataDirectory,
        Dispatcher dispatcher,
        SnipAssetCatalog snipAssetCatalog,
        MiraInspectionCatalog miraInspectionCatalog)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _runtimeManager = runtimeManager ?? throw new ArgumentNullException(nameof(runtimeManager));
        _flowsDirectory = flowsDirectory ?? throw new ArgumentNullException(nameof(flowsDirectory));
        _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _snipAssetCatalog = snipAssetCatalog ?? throw new ArgumentNullException(nameof(snipAssetCatalog));
        _miraInspectionCatalog = miraInspectionCatalog ?? throw new ArgumentNullException(nameof(miraInspectionCatalog));
        _nativeBundledFlowIds = LoadNativeBundledFlowIds();
        _securitySettingsStore = new SecuritySettingsStore(_dataDirectory);
        _securityAuditLogPath = Path.Combine(_dataDirectory, "logs", "security-audit.log");
        _flowInvocationService = new FlowInvocationService(
            _registry,
            _runtimeManager,
            _flowsDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed-flows"));
        _runtimeManager.FlowInvocationService = _flowInvocationService;
        _macroRecorderService.StopHotkeyPressed += OnMacroRecorderStopHotkeyPressed;

        Directory.CreateDirectory(_flowsDirectory);

    }

    public void Dispose()
    {
        _macroRecorderService.StopHotkeyPressed -= OnMacroRecorderStopHotkeyPressed;
        _macroRecorderService.Dispose();
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

            case "listRunnableFlows":
                await HandleListRunnableFlowsAsync(message);
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

            case "listRecipeCatalog":
                await HandleListRecipeCatalogAsync(message);
                break;

            case "analyzeFlowExperience":
                await HandleAnalyzeFlowExperienceAsync(message);
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

        if (TryEnrichVariablesFromBundledSeed(flow))
        {
            await FlowSerializer.SaveAsync(flow, filePath);
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

        foreach (var flow in flows)
        {
            TryEnrichVariablesFromBundledSeed(flow);
        }

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
            isNative = IsNativeFlowId(f.Id),
            preflightStatus = ResolvePreflightStatus(f),
            preflightMessage = ResolvePreflightMessage(f)
        }).OrderByDescending(f => f.modifiedAt).ToArray();

        Log($"Listed {summaries.Length} flows");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, summaries);
        }
    }

    private async Task HandleListRunnableFlowsAsync(BridgeMessage message)
    {
        var query = DeserializePayload<RunnableFlowQuery>(message.Payload) ?? new RunnableFlowQuery();
        var summaries = await _flowInvocationService.ListRunnableFlowsAsync(query);

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, summaries);
        }
    }

    private async Task HandleListRecipeCatalogAsync(BridgeMessage message)
    {
        var catalogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed-flows", "recipes.catalog.json");
        if (!File.Exists(catalogPath))
        {
            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, Array.Empty<object>());
            }
            return;
        }

        var catalog = JsonSerializer.Deserialize<List<RecipeCatalogItem>>(await File.ReadAllTextAsync(catalogPath), JsonOptions)
            ?? [];

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, catalog);
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

    private async Task HandleAnalyzeFlowExperienceAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);
        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var report = new FlowExperienceAnalyzer(_registry).Analyze(flow);
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, report);
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
        var security = AnalyzeFlowSecurity(flow);
        if (!security.IsSafeToRun && !IsHighRiskExecutionAcknowledged(message.Payload, flow, security, "exportRunnerPackage"))
        {
            await SendErrorIfRequested(
                message,
                "Flow blocked by security policy. Ative 'Permitir execucao com risco elevado' nas definicoes, confirme no editor e reenvie com securityAck.manifestHash, ou revise os nodes de risco.");
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

        var securityManifestPath = Path.Combine(packageDirectory, "security-manifest.json");
        await File.WriteAllTextAsync(securityManifestPath, JsonSerializer.Serialize(new
        {
            flowId = flow.Id,
            flowName = flow.Name,
            generatedAt = DateTime.UtcNow,
            riskLevel = security.RiskLevel,
            manifestHash = security.ManifestHash,
            issues = security.Issues
        }, JsonOptions));

        var integrityManifestPath = Path.Combine(packageDirectory, "integrity-manifest.json");
        var integrityEntries = Directory
            .EnumerateFiles(packageDirectory, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("integrity-manifest.json", StringComparison.OrdinalIgnoreCase))
            .Select(path => new
            {
                file = Path.GetRelativePath(packageDirectory, path).Replace('\\', '/'),
                sha256 = ComputeFileSha256(path)
            })
            .ToArray();
        await File.WriteAllTextAsync(integrityManifestPath, JsonSerializer.Serialize(new
        {
            generatedAt = DateTime.UtcNow,
            files = integrityEntries
        }, JsonOptions));

        var commandPath = Path.Combine(packageDirectory, "run-sidekick-flow.cmd");
        var preflightPath = Path.Combine(packageDirectory, "preflight.ps1");
        await File.WriteAllTextAsync(preflightPath, """
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$integrityPath = Join-Path $dir 'integrity-manifest.json'
$securityPath = Join-Path $dir 'security-manifest.json'
$consentPath = Join-Path $dir '.runner-consent.json'

if (-not (Test-Path $integrityPath)) { throw 'integrity-manifest.json nao encontrado.' }
if (-not (Test-Path $securityPath)) { throw 'security-manifest.json nao encontrado.' }

$integrity = Get-Content -Raw $integrityPath | ConvertFrom-Json
foreach ($entry in $integrity.files) {
  $target = Join-Path $dir $entry.file
  if (-not (Test-Path $target)) { throw "Arquivo ausente: $($entry.file)" }
  $hash = (Get-FileHash -Path $target -Algorithm SHA256).Hash
  if ($hash -ne $entry.sha256) { throw "Integridade invalida: $($entry.file)" }
}

$security = Get-Content -Raw $securityPath | ConvertFrom-Json
$manifestHash = [string]$security.manifestHash
$consentAccepted = $false
if (Test-Path $consentPath) {
  $consent = Get-Content -Raw $consentPath | ConvertFrom-Json
  if ($consent.manifestHash -eq $manifestHash -and $consent.accepted -eq $true) { $consentAccepted = $true }
}

if (-not $consentAccepted) {
  Add-Type -AssemblyName System.Windows.Forms | Out-Null
  $issueLines = @()
  if ($null -ne $security.issues) {
    foreach ($it in @($security.issues)) {
      if ($null -ne $it -and $it.message) { $issueLines += "- $($it.message)" }
    }
  }
  $issueSummary = if ($issueLines.Count -gt 0) { ($issueLines -join "`n") } else { '(sem detalhe adicional)' }
  $msg = "Este pacote Sidekick vai executar um fluxo automaticamente.`n`nNivel de risco: $($security.riskLevel).`n`n$issueSummary`n`nDeseja continuar?"
  $result = [System.Windows.Forms.MessageBox]::Show($msg, 'Sidekick Runner - Consentimento', [System.Windows.Forms.MessageBoxButtons]::OKCancel, [System.Windows.Forms.MessageBoxIcon]::Warning)
  if ($result -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Consentimento negado.' }
  @{ accepted = $true; manifestHash = $manifestHash; acceptedAt = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content -Path $consentPath
}
""");
        await File.WriteAllTextAsync(commandPath,
            "@echo off\r\n" +
            "set DIR=%~dp0\r\n" +
            "powershell -ExecutionPolicy Bypass -Sta -File \"%DIR%preflight.ps1\" || exit /b 1\r\n" +
            "\"%DIR%Sidekick.exe\" --run-flow \"%DIR%flow.json\" --exit-after-run\r\n");

        await File.WriteAllTextAsync(Path.Combine(packageDirectory, "README.txt"),
            "Sidekick Export Runner\r\n\r\n" +
            "Execute run-sidekick-flow.cmd para rodar este flow fora do editor visual.\r\n" +
            "Limite honesto: se uma instancia do Sidekick ja estiver aberta, o mutex de instancia unica pode bloquear a execucao autonomoma.\r\n" +
            "O runner executa preflight de integridade e consentimento (dialogo na primeira execucao) em preflight.ps1.\r\n" +
            "Logs ficam em %AppData%\\Sidekick\\logs.\r\n");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                success = true,
                packageDirectory,
                flowPath,
                commandPath,
                securityManifestPath,
                integrityManifestPath,
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

            case "runSavedFlow":
                await HandleRunSavedFlowAsync(message, forceAllowHighRisk: false);
                break;

            case "confirmPendingFlowInvocation":
                await HandleRunSavedFlowAsync(message, forceAllowHighRisk: true);
                break;

            case "validateFlow":
                await HandleValidateFlowAsync(message);
                break;

            case "securityLint":
                await HandleSecurityLintAsync(message);
                break;

            case "dryRunFlow":
                await HandleDryRunFlowAsync(message);
                break;

            case "killSwitch":
                await HandleKillSwitchAsync(message);
                break;

            case "stopFlow":
                await HandleStopFlowAsync(message);
                break;

            case "clearQueue":
                await HandleClearQueueAsync(message);
                break;

            case "restartFlow":
                await HandleRestartFlowAsync(message);
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

            case "getSecuritySettings":
                await HandleGetSecuritySettingsAsync(message);
                break;

            case "setSecuritySettings":
                await HandleSetSecuritySettingsAsync(message);
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
        var security = AnalyzeFlowSecurity(flow);
        if (!validation.IsValid)
        {
            Log($"Flow run blocked by validation: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    queued = false,
                    flowId = flow.Id,
                    validation,
                    security
                });
            }

            return;
        }

        if (!security.IsSafeToRun && !IsHighRiskExecutionAcknowledged(message.Payload, flow, security, "runFlow"))
        {
            Log($"Flow run blocked by security lint: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    queued = false,
                    flowId = flow.Id,
                    validation,
                    security
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
                validation,
                security
            });
        }
    }

    private async Task HandleRunSavedFlowAsync(BridgeMessage message, bool forceAllowHighRisk)
    {
        var request = BuildFlowInvocationRequest(message.Payload, forceAllowHighRisk);
        var result = await _flowInvocationService.QueueFlowAsync(request);
        Log($"Saved flow invocation: {result.Status} {result.FlowName} ({result.FlowId})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
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

    private async Task HandleSecurityLintAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);
        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var security = AnalyzeFlowSecurity(flow);
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, security);
        }
    }

    private async Task HandleDryRunFlowAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);
        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var report = new FlowDryRunPlanner(_registry).CreateReport(flow);
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, report);
        }
    }

    private async Task HandleKillSwitchAsync(BridgeMessage message)
    {
        var result = _runtimeManager.Stop(StopFlowMode.CancelAll);
        await _runtimeManager.DeactivateAllAsync();
        Log("Global kill switch requested: current run cancelled, queue cleared and armed flows deactivated.");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                stopped = true,
                result
            });
        }
    }

    private async Task HandleGetSecuritySettingsAsync(BridgeMessage message)
    {
        var settings = _securitySettingsStore.Load();
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, settings);
        }
    }

    private async Task HandleSetSecuritySettingsAsync(BridgeMessage message)
    {
        if (message.Payload == null || message.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            await SendErrorIfRequested(message, "Invalid security settings payload");
            return;
        }

        var incoming = JsonSerializer.Deserialize<SecuritySettings>(message.Payload.Value.GetRawText(), JsonOptions)
                       ?? new SecuritySettings();
        var current = _securitySettingsStore.Load();
        current.AllowHighRiskExecution = incoming.AllowHighRiskExecution;
        _securitySettingsStore.Save(current);

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, current);
        }
    }

    /// <summary>
    /// When the flow is not safe, allows proceed only if the user enabled high-risk mode in AppData
    /// and the payload carries a <c>securityAck.manifestHash</c> matching the current security report.
    /// </summary>
    private bool IsHighRiskExecutionAcknowledged(JsonElement? payload, Flow flow, SecurityReport security, string actionLabel)
    {
        if (security.IsSafeToRun)
        {
            return true;
        }

        SecuritySettings settings;
        try
        {
            settings = _securitySettingsStore.Load();
        }
        catch (Exception ex)
        {
            Log($"Security settings load failed: {ex.Message}");
            return false;
        }

        if (!settings.AllowHighRiskExecution)
        {
            return false;
        }

        if (!TryGetSecurityAckManifestHash(payload, out var ackHash) ||
            !string.Equals(ackHash, security.ManifestHash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var auditDirectory = Path.GetDirectoryName(_securityAuditLogPath);
            if (!string.IsNullOrEmpty(auditDirectory))
            {
                Directory.CreateDirectory(auditDirectory);
            }

            var codes = string.Join(",", security.Issues.Select(i => i.Code));
            File.AppendAllText(
                _securityAuditLogPath,
                $"[{DateTime.UtcNow:O}] action={actionLabel} flowId={flow.Id} flowName={flow.Name} manifestHash={security.ManifestHash} codes={codes}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            Log($"Security audit append failed: {ex.Message}");
        }

        return true;
    }

    private static bool TryGetSecurityAckManifestHash(JsonElement? payload, out string? manifestHash)
    {
        manifestHash = null;
        if (payload == null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.Value.TryGetProperty("securityAck", out var ack) || ack.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!ack.TryGetProperty("manifestHash", out var hashElement) || hashElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        manifestHash = hashElement.GetString();
        return !string.IsNullOrWhiteSpace(manifestHash);
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

    private async Task HandleClearQueueAsync(BridgeMessage message)
    {
        var flowId = GetPayloadString(message.Payload, "flowId");
        var result = _runtimeManager.ClearQueue(flowId);
        Log(string.IsNullOrWhiteSpace(flowId)
            ? $"Runtime queue clear requested: {result.ClearedQueuedRuns} pending run(s) removed."
            : $"Runtime queue clear requested for {flowId}: {result.ClearedQueuedRuns} pending run(s) removed.");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
        }
    }

    private async Task HandleRestartFlowAsync(BridgeMessage message)
    {
        var flow = await ResolveFlowAsync(message.Payload);

        if (flow == null)
        {
            await SendErrorIfRequested(message, "No valid flow provided");
            return;
        }

        var validation = ValidateFlow(flow);
        var security = AnalyzeFlowSecurity(flow);
        if (!validation.IsValid)
        {
            Log($"Flow restart blocked by validation: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    queued = false,
                    restarted = false,
                    flowId = flow.Id,
                    validation,
                    security
                });
            }

            return;
        }

        if (!security.IsSafeToRun && !IsHighRiskExecutionAcknowledged(message.Payload, flow, security, "restartFlow"))
        {
            Log($"Flow restart blocked by security lint: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    queued = false,
                    restarted = false,
                    flowId = flow.Id,
                    validation,
                    security
                });
            }

            return;
        }

        var result = _runtimeManager.RestartManualRun(flow);
        Log($"Flow restart requested: {flow.Name} ({flow.Id}) - cancelledCurrent={result.CancelledCurrentRun}, clearedQueued={result.ClearedQueuedRuns}");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                queued = result.Queued,
                restarted = true,
                flowId = flow.Id,
                queueLength = result.QueueEvent.QueueLength,
                queuePending = result.QueueEvent.QueuePending,
                cancelledCurrentRun = result.CancelledCurrentRun,
                clearedQueuedRuns = result.ClearedQueuedRuns,
                remainingQueueLength = result.RemainingQueueLength,
                validation,
                security
            });
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
        var security = AnalyzeFlowSecurity(flow);
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
                    validation,
                    security
                });
            }

            return;
        }

        if (!security.IsSafeToRun && !IsHighRiskExecutionAcknowledged(message.Payload, flow, security, "activateFlow"))
        {
            Log($"Flow activation blocked by security lint: {flow.Name} ({flow.Id})");

            if (message.RequestId != null)
            {
                await _bridge.SendResponseAsync(message.RequestId, new
                {
                    armed = false,
                    flow = (object?)null,
                    warnings = validation.Warnings,
                    validation,
                    security
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
                validation,
                security
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

        var diskFlowPath = await FindFlowFilePathAsync(flowId);
        var diskFlow = await FlowSerializer.LoadAsync(diskFlowPath);
        if (diskFlow != null)
        {
            if (TryEnrichVariablesFromBundledSeed(diskFlow))
            {
                await FlowSerializer.SaveAsync(diskFlow, diskFlowPath);
            }
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

    private SecurityReport AnalyzeFlowSecurity(Flow flow)
    {
        var analyzer = new FlowSecurityAnalyzer();
        return analyzer.Analyze(flow);
    }

    private string ResolvePreflightStatus(Flow flow)
    {
        var validation = ValidateFlow(flow);
        if (!validation.IsValid)
        {
            return validation.Errors.Any(IsConfigurationValidationError)
                ? "needsConfiguration"
                : "blocked";
        }

        var security = AnalyzeFlowSecurity(flow);
        return security.IsSafeToRun ? "ready" : "blocked";
    }

    private string ResolvePreflightMessage(Flow flow)
    {
        var validation = ValidateFlow(flow);
        if (!validation.IsValid)
        {
            var prefix = validation.Errors.Any(IsConfigurationValidationError)
                ? "Precisa configurar"
                : "Bloqueado";
            return $"{prefix}: {string.Join(" | ", validation.Errors.Take(2))}";
        }

        var security = AnalyzeFlowSecurity(flow);
        if (!security.IsSafeToRun)
        {
            return $"Bloqueado por seguranca: {string.Join(" | ", security.Issues.Take(2).Select(issue => issue.Message))}";
        }

        return "Pronto para uso";
    }

    private static bool IsConfigurationValidationError(string error)
    {
        return error.Contains("required property", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("image template", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("incomplete selector", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("unresolved reference", StringComparison.OrdinalIgnoreCase);
    }

    private static FlowInvocationRequest BuildFlowInvocationRequest(JsonElement? payload, bool forceAllowHighRisk)
    {
        var payloadFlowId = GetPayloadString(payload, "flowId") ?? GetPayloadString(payload, "id") ?? "";
        var payloadSource = GetPayloadString(payload, "source") ?? "local";
        var requestedBy = GetPayloadString(payload, "requestedBy") ?? "";
        var correlationId = GetPayloadString(payload, "correlationId") ?? Guid.NewGuid().ToString("n");
        var currentFlowId = GetPayloadString(payload, "currentFlowId");
        var allowedFlowIds = GetPayloadStringArray(payload, "allowedFlowIds") ?? [];
        var allowHighRisk = forceAllowHighRisk || GetPayloadBool(payload, "allowHighRisk");

        return new FlowInvocationRequest
        {
            FlowId = payloadFlowId,
            Source = payloadSource,
            RequestedBy = requestedBy,
            AllowHighRisk = allowHighRisk,
            CorrelationId = correlationId,
            CurrentFlowId = currentFlowId,
            AllowedFlowIds = allowedFlowIds
        };
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

            case "browseFile":
                await HandleBrowseFileAsync(message);
                break;

            case "browseFolder":
                await HandleBrowseFolderAsync(message);
                break;

            case "startMacroRecorder":
                await HandleStartMacroRecorderAsync(message);
                break;

            case "getMacroRecorderStatus":
                await HandleGetMacroRecorderStatusAsync(message);
                break;

            case "stopMacroRecorder":
                await HandleStopMacroRecorderAsync(message);
                break;

            case "cancelMacroRecorder":
                await HandleCancelMacroRecorderAsync(message);
                break;

            case "convertMacroDraftToFlow":
                await HandleConvertMacroDraftToFlowAsync(message);
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

            case "updateSnipAsset":
                await HandleUpdateSnipAssetAsync(message);
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

            case "diagnoseInspectionAsset":
                await HandleDiagnoseInspectionAssetAsync(message);
                break;

            case "diagnoseSelector":
                await HandleDiagnoseSelectorAsync(message);
                break;

            case "diagnoseSelectorBatch":
                await HandleDiagnoseSelectorBatchAsync(message);
                break;

            default:
                await SendErrorIfRequested(message, $"Unknown assets action: {message.Action}");
                break;
        }
    }

    private async Task HandleBrowseFileAsync(BridgeMessage message)
    {
        var currentPath = GetPayloadString(message.Payload, "currentPath");
        var propertyName = GetPayloadString(message.Payload, "propertyName");

        var result = await _dispatcher.InvokeAsync(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = string.IsNullOrWhiteSpace(propertyName)
                    ? "Selecionar arquivo"
                    : $"Selecionar arquivo - {propertyName}",
                CheckFileExists = true,
                Multiselect = false,
                Filter = "Todos os arquivos (*.*)|*.*"
            };

            var initialDirectory = ResolveDialogInitialDirectory(currentPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            if (!string.IsNullOrWhiteSpace(currentPath) && File.Exists(currentPath))
            {
                dialog.FileName = currentPath;
            }

            var accepted = dialog.ShowDialog();
            return accepted == true
                ? new BrowsePathResult { Path = dialog.FileName, Cancelled = false }
                : new BrowsePathResult { Cancelled = true };
        });

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
        }
    }

    private async Task HandleBrowseFolderAsync(BridgeMessage message)
    {
        var currentPath = GetPayloadString(message.Payload, "currentPath");
        var propertyName = GetPayloadString(message.Payload, "propertyName");

        var result = await _dispatcher.InvokeAsync(() =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = string.IsNullOrWhiteSpace(propertyName)
                    ? "Selecionar pasta"
                    : $"Selecionar pasta - {propertyName}"
            };

            var initialDirectory = ResolveDialogInitialDirectory(currentPath);
            if (!string.IsNullOrWhiteSpace(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            var accepted = dialog.ShowDialog();
            return accepted == true
                ? new BrowsePathResult { Path = dialog.FolderName, Cancelled = false }
                : new BrowsePathResult { Cancelled = true };
        });

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
        }
    }

    private async Task HandleStartMacroRecorderAsync(BridgeMessage message)
    {
        var options = DeserializePayload<MacroRecorderOptions>(message.Payload) ?? new MacroRecorderOptions();
        options.Goal ??= GetPayloadString(message.Payload, "goal");
        var session = _macroRecorderService.Start(options);

        Log($"Macro recorder started with hooks (session {session.SessionId})");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                started = true,
                sessionId = session.SessionId,
                startedAt = session.StartedAt,
                status = session.Status,
                eventCount = session.EventCount,
                privacyMode = session.PrivacyMode,
                mode = "draft",
                message = "Gravador global em modo rascunho: nenhuma acao sera executada automaticamente."
            });
        }
    }

    private async Task HandleGetMacroRecorderStatusAsync(BridgeMessage message)
    {
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, _macroRecorderService.GetStatus());
        }
    }

    private async Task HandleStopMacroRecorderAsync(BridgeMessage message)
    {
        var result = _macroRecorderService.Stop();
        var draft = MacroDraftBuilder.BuildDraft(
            result.Session,
            result.Events,
            new MacroRecorderOptions { CaptureSensitiveText = result.Session.PrivacyMode == "captureSensitive" });
        _lastMacroDraft = draft;
        Log($"Macro recorder stopped with {draft.Events.Count} normalized event(s) and {Math.Max(0, draft.SuggestedNodes.Count - 1)} suggested step(s)");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, draft);
        }
    }

    private async Task HandleCancelMacroRecorderAsync(BridgeMessage message)
    {
        _macroRecorderService.Cancel();
        _lastMacroDraft = null;
        Log("Macro recorder cancelled and buffer discarded");

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                cancelled = true,
                status = _macroRecorderService.GetStatus()
            });
        }
    }

    private async Task HandleConvertMacroDraftToFlowAsync(BridgeMessage message)
    {
        var draft = DeserializeMacroDraft(message.Payload) ?? _lastMacroDraft;
        if (draft is null)
        {
            await SendErrorIfRequested(message, "No macro draft available to convert");
            return;
        }

        var converted = MacroDraftBuilder.RebuildDraftFromEditedEvents(draft);
        _lastMacroDraft = converted;
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, converted);
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

    private async Task HandleUpdateSnipAssetAsync(BridgeMessage message)
    {
        var assetId = GetPayloadString(message.Payload, "assetId") ?? GetPayloadString(message.Payload, "id");
        if (string.IsNullOrWhiteSpace(assetId))
        {
            await SendErrorIfRequested(message, "No snip asset id specified");
            return;
        }

        var displayName = GetPayloadString(message.Payload, "displayName");
        var notes = GetPayloadString(message.Payload, "notes");
        var ocrText = GetPayloadString(message.Payload, "ocrText");
        var tags = GetPayloadStringArray(message.Payload, "tags");
        var asset = await _snipAssetCatalog.UpdateAsync(assetId, displayName, notes, tags, ocrText);
        if (asset is null)
        {
            await SendErrorIfRequested(message, $"Snip asset not found: {assetId}");
            return;
        }

        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, asset);
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

    private async Task HandleDiagnoseInspectionAssetAsync(BridgeMessage message)
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

        var selector = new SelectorDiagnosticRequest
        {
            WindowTitle = asset.Locator.Selector.WindowTitle ?? asset.Source.WindowTitle,
            AutomationId = asset.Locator.Selector.AutomationId,
            Name = asset.Locator.Selector.Name,
            ControlType = asset.Locator.Selector.ControlType,
            ProcessName = asset.Source.ProcessName,
            ProcessPath = asset.Source.ProcessPath,
            TitleMatch = "contains",
            CapturedStrength = asset.Locator.Strength,
            CapturedStrengthReason = asset.Locator.StrengthReason,
            HasRelativeFallback = asset.Locator.Fallback.UseRelativeFallback,
            HasVisualFallback = asset.Locator.Fallback.UseAbsoluteFallback
        };

        var result = DiagnoseSelector(selector);
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
        }
    }

    private async Task HandleDiagnoseSelectorAsync(BridgeMessage message)
    {
        var request = DeserializePayload<SelectorDiagnosticRequest>(message.Payload) ?? new SelectorDiagnosticRequest();
        var result = DiagnoseSelector(request);
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, result);
        }
    }

    private async Task HandleDiagnoseSelectorBatchAsync(BridgeMessage message)
    {
        var requests = DeserializeSelectorBatch(message.Payload);
        var results = requests.Select(DiagnoseSelector).ToArray();
        if (message.RequestId != null)
        {
            await _bridge.SendResponseAsync(message.RequestId, new
            {
                testedAt = DateTime.UtcNow,
                count = results.Length,
                results
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

    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
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
    /// Merges <see cref="Flow.Variables"/> from the bundled seed JSON when the user copy on disk
    /// is stale. Seeding uses overwrite:false, so AppData files may miss variables added later
    /// even when they already contain older variables.
    /// </summary>
    private bool TryEnrichVariablesFromBundledSeed(Flow flow)
    {
        if (string.IsNullOrWhiteSpace(flow.Id) || !IsNativeFlowId(flow.Id))
        {
            return false;
        }

        var bundledFlowsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "seed-flows");
        if (!Directory.Exists(bundledFlowsDirectory))
        {
            return false;
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

                if (BundledFlowUpdater.RefreshFromSeedIfNewer(flow, seed))
                {
                    Log($"Refreshed native flow '{flow.Id}' from bundled seed version {seed.Version}.");
                    return true;
                }

                var added = BundledFlowUpdater.MergeMissingVariables(flow, seed);

                if (added > 0)
                {
                    Log($"Enriched {added} missing flow variable(s) from bundled seed for native flow '{flow.Id}'.");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log($"Bundled seed parse skipped for '{Path.GetFileName(sourcePath)}': {ex.Message}");
            }
        }

        return false;
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

    private static bool GetPayloadBool(JsonElement? payload, string propertyName)
    {
        if (payload == null || payload.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!payload.Value.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(prop.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static string? ResolveDialogInitialDirectory(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return null;
        }

        if (Directory.Exists(currentPath))
        {
            return currentPath;
        }

        var directory = Path.GetDirectoryName(currentPath);
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? directory
            : null;
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

    private static GuidedAutomationDraft? DeserializeMacroDraft(JsonElement? payload)
    {
        if (payload == null)
        {
            return null;
        }

        if (payload.Value.ValueKind == JsonValueKind.Object &&
            payload.Value.TryGetProperty("draft", out var draftElement))
        {
            return JsonSerializer.Deserialize<GuidedAutomationDraft>(draftElement.GetRawText(), JsonOptions);
        }

        return JsonSerializer.Deserialize<GuidedAutomationDraft>(payload.Value.GetRawText(), JsonOptions);
    }

    private static IReadOnlyList<SelectorDiagnosticRequest> DeserializeSelectorBatch(JsonElement? payload)
    {
        if (payload == null)
        {
            return [];
        }

        if (payload.Value.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<SelectorDiagnosticRequest>>(payload.Value.GetRawText(), JsonOptions) ?? [];
        }

        if (payload.Value.ValueKind == JsonValueKind.Object &&
            payload.Value.TryGetProperty("selectors", out var selectorsElement) &&
            selectorsElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<SelectorDiagnosticRequest>>(selectorsElement.GetRawText(), JsonOptions) ?? [];
        }

        var single = JsonSerializer.Deserialize<SelectorDiagnosticRequest>(payload.Value.GetRawText(), JsonOptions);
        return single is null ? [] : [single];
    }

    private static object DiagnoseSelector(SelectorDiagnosticRequest request)
    {
        var startedAt = DateTime.UtcNow;
        var titleMatch = AutomationElementLocator.ParseTitleMatch(request.TitleMatch);
        var found = AutomationElementLocator.FindElement(
            request.WindowTitle,
            request.AutomationId,
            request.Name ?? request.ElementName,
            request.ControlType,
            Math.Clamp(request.TimeoutMs <= 0 ? 1000 : request.TimeoutMs, 100, 5000),
            request.ProcessName,
            request.ProcessPath,
            titleMatch);

        var rect = found?.Current.BoundingRectangle;
        var strength = ResolveSelectorStrength(request, found != null);
        var reason = ResolveSelectorReason(request, found != null, request.CapturedStrengthReason);
        var fallback = ResolveSelectorFallback(request, found != null);

        return new
        {
            testedAt = startedAt,
            found = found != null,
            strength,
            reason,
            fallbackRecommendation = fallback,
            repairAction = found == null || strength == "fraca" ? "repairWithLatestCapture" : "none",
            bounds = found == null || rect == null
                ? null
                : new
                {
                    x = (int)rect.Value.X,
                    y = (int)rect.Value.Y,
                    width = (int)rect.Value.Width,
                    height = (int)rect.Value.Height
                },
            selector = new
            {
                windowTitle = request.WindowTitle,
                automationId = request.AutomationId,
                name = request.Name ?? request.ElementName,
                controlType = request.ControlType,
                processName = request.ProcessName,
                processPath = request.ProcessPath,
                titleMatch = request.TitleMatch
            }
        };
    }

    private static string ResolveSelectorStrength(SelectorDiagnosticRequest request, bool found)
    {
        if (!found)
        {
            return "inexistente";
        }

        var hasStableElement = !string.IsNullOrWhiteSpace(request.AutomationId);
        var hasWindowScope = !string.IsNullOrWhiteSpace(request.WindowTitle)
            || !string.IsNullOrWhiteSpace(request.ProcessName)
            || !string.IsNullOrWhiteSpace(request.ProcessPath);
        var hasSemanticElement = !string.IsNullOrWhiteSpace(request.Name)
            || !string.IsNullOrWhiteSpace(request.ElementName)
            || !string.IsNullOrWhiteSpace(request.ControlType);

        if (hasStableElement && hasWindowScope)
        {
            return "forte";
        }

        if (hasStableElement || (hasSemanticElement && hasWindowScope))
        {
            return "media";
        }

        return "fraca";
    }

    private static string ResolveSelectorReason(SelectorDiagnosticRequest request, bool found, string? capturedReason)
    {
        if (!found)
        {
            return "O elemento nao foi encontrado agora com os criterios salvos.";
        }

        if (!string.IsNullOrWhiteSpace(capturedReason))
        {
            return capturedReason;
        }

        if (!string.IsNullOrWhiteSpace(request.AutomationId))
        {
            return "AutomationId encontrado no escopo de janela/processo atual.";
        }

        return "Encontrado por nome/tipo; pode variar se a janela mudar idioma, layout ou estado.";
    }

    private static string ResolveSelectorFallback(SelectorDiagnosticRequest request, bool found)
    {
        if (!found)
        {
            return request.HasRelativeFallback || request.HasVisualFallback
                ? "Use fallback relativo/visual salvo e recapture com Mira se a janela mudou."
                : "Recapture com Mira e salve fallback relativo antes de monitorar.";
        }

        if (string.Equals(ResolveSelectorStrength(request, found), "forte", StringComparison.OrdinalIgnoreCase))
        {
            return "Manter seletor atual; dry-run antes de armar e recomendado.";
        }

        return "Reparar com a ultima captura Mira para adicionar AutomationId, processo ou fallback relativo.";
    }

    private sealed class ExecutionHistoryRequest
    {
        public int Limit { get; set; } = 50;
        public string? FlowId { get; set; }
    }

    private sealed class RecipeCatalogItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Persona { get; set; } = "";
        public string Risk { get; set; } = "low";
        public int Popularity { get; set; }
        public string[] Tags { get; set; } = [];
    }

    private sealed class BrowsePathResult
    {
        public string? Path { get; set; }
        public bool Cancelled { get; set; }
    }

    private sealed class SelectorDiagnosticRequest
    {
        public string? WindowTitle { get; set; }
        public string? AutomationId { get; set; }
        public string? Name { get; set; }
        public string? ElementName { get; set; }
        public string? ControlType { get; set; }
        public string? ProcessName { get; set; }
        public string? ProcessPath { get; set; }
        public string? TitleMatch { get; set; } = "contains";
        public int TimeoutMs { get; set; } = 1000;
        public string? CapturedStrength { get; set; }
        public string? CapturedStrengthReason { get; set; }
        public bool HasRelativeFallback { get; set; }
        public bool HasVisualFallback { get; set; }
    }

    private void OnMacroRecorderStopHotkeyPressed(object? sender, MacroRecorderHotkeyEventArgs e)
    {
        _ = _bridge.SendEventAsync(BridgeMessage.Channels.Platform, "macroRecorderStopRequested", new
        {
            hotkey = e.Hotkey,
            status = _macroRecorderService.GetStatus(),
            message = "Hotkey de parada detectada. Confirme a revisao para finalizar o rascunho."
        });
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
