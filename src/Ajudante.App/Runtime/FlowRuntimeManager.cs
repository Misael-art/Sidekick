using Ajudante.Core;
using Ajudante.Core.Engine;
using Ajudante.Core.Interfaces;
using Ajudante.Core.Serialization;

namespace Ajudante.App.Runtime;

public sealed class FlowRuntimeManager : IDisposable
{
    private readonly object _sync = new();
    private readonly FlowExecutor _executor;
    private readonly TriggerManager _triggerManager;
    private readonly IExecutionHistoryStore? _executionHistoryStore;
    private readonly Queue<QueuedFlowRun> _queue = new();
    private readonly Dictionary<string, FlowRuntimeEntry> _flowStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly Task _workerTask;

    private bool _disposed;
    private QueuedFlowRun? _currentRun;
    private RunCompletionState _currentRunCompletionState;
    private string? _currentRunError;
    private bool _currentRunStopRequested;
    private ActiveRunHistory? _currentRunHistory;

    public event Action<string, NodeStatus>? NodeStatusChanged;
    public event Action<string, string>? LogMessage;
    public event Action<string>? FlowCompleted;
    public event Action<string, string>? FlowError;
    public event EventHandler<RuntimeStatusSnapshot>? RuntimeStatusChanged;
    public event EventHandler<FlowRuntimeSnapshot>? FlowArmed;
    public event EventHandler<FlowRuntimeSnapshot>? FlowDisarmed;
    public event EventHandler<TriggerRuntimeEvent>? TriggerFired;
    public event EventHandler<FlowQueuedEvent>? FlowQueued;
    public event EventHandler<FlowQueuedEvent>? FlowQueueCoalesced;
    public event EventHandler<RuntimeErrorEvent>? RuntimeError;
    public event EventHandler<FlowExecutionHistoryEntry>? ExecutionHistoryRecorded;
    public event EventHandler<RuntimePhaseEvent>? RuntimePhaseChanged;

    public bool IsRunning => _executor.IsRunning;

    public FlowRuntimeManager(INodeRegistry registry, IExecutionHistoryStore? executionHistoryStore = null)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _executor = new FlowExecutor(registry);
        _triggerManager = new TriggerManager(registry);
        _executionHistoryStore = executionHistoryStore;

        _executor.NodeStatusChanged += OnNodeStatusChanged;
        _executor.LogMessage += OnExecutorLogMessage;
        _executor.FlowCompleted += OnFlowCompleted;
        _executor.FlowError += OnFlowError;
        _executor.PhaseChanged += OnPhaseChanged;

        _triggerManager.TriggerFired += OnTriggerFired;
        _triggerManager.LogMessage += OnTriggerManagerLogMessage;
        _triggerManager.TriggerError += OnTriggerManagerError;

        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public FlowQueuedEvent QueueManualRun(Flow flow)
    {
        ThrowIfDisposed();
        return EnqueueRun(CloneFlow(flow), FlowRunSource.Manual, null, null);
    }

    public async Task<FlowActivationResult> ActivateFlowAsync(Flow flow)
    {
        ThrowIfDisposed();

        var normalizedFlow = CloneFlow(flow);
        EnsureFlowIdentity(normalizedFlow);

        var activation = await _triggerManager.ActivateFlowTriggersAsync(normalizedFlow);
        if (activation.ActiveTriggerNodeIds.Length == 0)
        {
            var error = activation.Errors.Length > 0
                ? string.Join(" | ", activation.Errors)
                : "Flow does not contain continuous triggers that can be armed.";

            RecordFlowError(normalizedFlow, error);
            throw new InvalidOperationException(error);
        }

        FlowRuntimeSnapshot snapshot;
        RuntimeErrorEvent? warningEvent = null;

        lock (_sync)
        {
            var entry = GetOrCreateEntry_NoLock(normalizedFlow.Id, normalizedFlow.Name);
            entry.Flow = normalizedFlow;
            entry.IsArmed = true;
            entry.ActiveTriggerNodeIds = activation.ActiveTriggerNodeIds.ToArray();
            entry.LastError = activation.Errors.Length > 0
                ? string.Join(" | ", activation.Errors)
                : null;

            RefreshPendingRuns_NoLock(normalizedFlow.Id, normalizedFlow);
            snapshot = CreateFlowSnapshot_NoLock(entry);

            if (!string.IsNullOrWhiteSpace(entry.LastError))
            {
                warningEvent = CreateRuntimeError_NoLock(entry, entry.LastError!);
            }
        }

        FlowArmed?.Invoke(this, snapshot);
        if (warningEvent != null)
        {
            RuntimeError?.Invoke(this, warningEvent);
        }

        EmitRuntimeStatusChanged();

        return new FlowActivationResult
        {
            Armed = true,
            Snapshot = snapshot,
            Warnings = activation.Errors
        };
    }

    public async Task<FlowRuntimeSnapshot?> DeactivateFlowAsync(string flowId)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return null;
        }

        await _triggerManager.DeactivateFlowAsync(flowId);

        FlowRuntimeSnapshot? snapshot = null;
        var shouldEmitDisarmed = false;

        lock (_sync)
        {
            if (!_flowStates.TryGetValue(flowId, out var entry))
            {
                return null;
            }

            shouldEmitDisarmed = entry.IsArmed || entry.ActiveTriggerNodeIds.Length > 0;
            entry.IsArmed = false;
            entry.ActiveTriggerNodeIds = [];
            snapshot = CreateFlowSnapshot_NoLock(entry);
            RemoveEntryIfInactive_NoLock(flowId, entry);
        }

        if (snapshot != null && shouldEmitDisarmed)
        {
            FlowDisarmed?.Invoke(this, snapshot);
        }

        EmitRuntimeStatusChanged();
        return snapshot;
    }

    public async Task DeactivateAllAsync()
    {
        ThrowIfDisposed();

        await _triggerManager.DeactivateAllAsync();

        lock (_sync)
        {
            foreach (var (flowId, entry) in _flowStates.ToArray())
            {
                entry.IsArmed = false;
                entry.ActiveTriggerNodeIds = [];
                RemoveEntryIfInactive_NoLock(flowId, entry);
            }
        }

        EmitRuntimeStatusChanged();
    }

    public StopFlowResult Stop(StopFlowMode mode)
    {
        ThrowIfDisposed();

        var cancelledCurrent = false;
        var clearedQueuedRuns = 0;
        RuntimeStatusSnapshot snapshot;

        lock (_sync)
        {
            if (mode == StopFlowMode.CancelAll)
            {
                clearedQueuedRuns = _queue.Count;
                ClearQueuedRuns_NoLock();
            }

            if (_currentRun != null)
            {
                _currentRunStopRequested = true;
                cancelledCurrent = true;
            }

            snapshot = CreateRuntimeStatusSnapshot_NoLock();
        }

        if (cancelledCurrent)
        {
            _executor.Cancel();
        }

        if (cancelledCurrent || clearedQueuedRuns > 0)
        {
            EmitRuntimeStatusChanged();
        }

        return new StopFlowResult
        {
            CancelledCurrentRun = cancelledCurrent,
            ClearedQueuedRuns = clearedQueuedRuns,
            RemainingQueueLength = snapshot.QueueLength,
            IsRunning = snapshot.IsRunning
        };
    }

    public RuntimeStatusSnapshot GetRuntimeStatus()
    {
        lock (_sync)
        {
            return CreateRuntimeStatusSnapshot_NoLock();
        }
    }

    public FlowRuntimeSnapshot[] ListActiveFlows()
    {
        lock (_sync)
        {
            return _flowStates.Values
                .Where(entry => entry.IsArmed)
                .Select(CreateFlowSnapshot_NoLock)
                .OrderBy(snapshot => snapshot.FlowName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public Task<FlowExecutionHistoryEntry[]> GetExecutionHistoryAsync(
        int limit = 50,
        string? flowId = null,
        CancellationToken cancellationToken = default)
    {
        if (_executionHistoryStore == null)
        {
            return Task.FromResult(Array.Empty<FlowExecutionHistoryEntry>());
        }

        return _executionHistoryStore.ListAsync(limit, flowId, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Stop(StopFlowMode.CancelAll);
        }
        catch
        {
            // Keep disposal resilient while shutting down the app.
        }

        _shutdownCts.Cancel();

        try
        {
            _triggerManager.DeactivateAllAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort cleanup during shutdown.
        }

        _executor.Cancel();

        try
        {
            _workerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // The queue worker is cancelled during shutdown and should not block process exit.
        }

        _executor.NodeStatusChanged -= OnNodeStatusChanged;
        _executor.LogMessage -= OnExecutorLogMessage;
        _executor.FlowCompleted -= OnFlowCompleted;
        _executor.FlowError -= OnFlowError;
        _executor.PhaseChanged -= OnPhaseChanged;

        _triggerManager.TriggerFired -= OnTriggerFired;
        _triggerManager.LogMessage -= OnTriggerManagerLogMessage;
        _triggerManager.TriggerError -= OnTriggerManagerError;

        _triggerManager.Dispose();
        _queueSignal.Dispose();
        _shutdownCts.Dispose();
        _disposed = true;
    }

    private async Task ProcessQueueAsync()
    {
        while (!_shutdownCts.IsCancellationRequested)
        {
            QueuedFlowRun? run = null;

            try
            {
                run = await DequeueNextRunAsync(_shutdownCts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                EmitRuntimeError(new RuntimeErrorEvent
                {
                    FlowId = "",
                    FlowName = "",
                    Error = $"Runtime queue failure: {ex.Message}"
                });
                continue;
            }

            if (run == null)
            {
                continue;
            }

            await ExecuteRunAsync(run);
        }
    }

    private async Task<QueuedFlowRun?> DequeueNextRunAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (_sync)
            {
                if (_queue.Count > 0)
                {
                    var run = _queue.Dequeue();
                    var startedAt = DateTime.UtcNow;
                    run.StartedAt = startedAt;

                    var entry = GetOrCreateEntry_NoLock(run.Flow.Id, run.Flow.Name);
                    entry.Flow = CloneFlow(run.Flow);
                    entry.IsRunning = true;
                    entry.QueuePending = HasPendingRun_NoLock(run.Flow.Id);
                    entry.LastRunAt = startedAt;

                    _currentRun = run;
                    _currentRunCompletionState = RunCompletionState.None;
                    _currentRunError = null;
                    _currentRunStopRequested = false;
                    _currentRunHistory = new ActiveRunHistory
                    {
                        RunId = Guid.NewGuid().ToString("n"),
                        FlowId = run.Flow.Id,
                        FlowName = run.Flow.Name,
                        Source = run.Source,
                        TriggerNodeId = run.TriggerNodeId,
                        StartedAt = startedAt
                    };

                    return run;
                }
            }

            await _queueSignal.WaitAsync(cancellationToken);
        }
    }

    private async Task ExecuteRunAsync(QueuedFlowRun run)
    {
        var runningHistoryEntry = CaptureCurrentRunHistoryEntry();
        if (runningHistoryEntry is { Result: FlowExecutionResult.Running })
        {
            await PersistExecutionHistoryAsync(runningHistoryEntry);
        }

        EmitRuntimeStatusChanged();

        try
        {
            if (run.Source == FlowRunSource.Trigger && run.TriggerNodeId != null)
            {
                await _executor.ExecuteFromTriggerAsync(
                    run.Flow,
                    run.TriggerNodeId,
                    run.TriggerData ?? new Dictionary<string, object?>(),
                    _shutdownCts.Token);
            }
            else
            {
                await _executor.ExecuteAsync(run.Flow);
            }
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                if (_currentRun != null && string.Equals(_currentRun.Flow.Id, run.Flow.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _currentRunCompletionState = RunCompletionState.Error;
                    _currentRunError = ex.Message;
                }
            }

            EmitRuntimeError(new RuntimeErrorEvent
            {
                FlowId = run.Flow.Id,
                FlowName = run.Flow.Name,
                Error = ex.Message
            });
        }
        finally
        {
            RuntimeErrorEvent? runtimeError = null;
            FlowExecutionHistoryEntry? completedHistoryEntry = null;

            lock (_sync)
            {
                if (!_flowStates.TryGetValue(run.Flow.Id, out var entry))
                {
                    entry = GetOrCreateEntry_NoLock(run.Flow.Id, run.Flow.Name);
                    entry.Flow = CloneFlow(run.Flow);
                }

                entry.IsRunning = false;
                entry.QueuePending = HasPendingRun_NoLock(run.Flow.Id);

                if (_currentRunCompletionState == RunCompletionState.Error && !string.IsNullOrWhiteSpace(_currentRunError))
                {
                    entry.LastError = _currentRunError;
                    runtimeError = CreateRuntimeError_NoLock(entry, _currentRunError!);
                }
                else if (_currentRunStopRequested)
                {
                    entry.LastError = null;
                    _currentRunHistory?.Logs.Add(new ExecutionHistoryLogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "info",
                        NodeId = run.Flow.Id,
                        Message = "Flow execution cancelled by stop request."
                    });
                }
                else if (_currentRunCompletionState == RunCompletionState.Completed)
                {
                    entry.LastError = null;
                }

                completedHistoryEntry = CaptureCurrentRunHistoryEntry_NoLock(DateTime.UtcNow);
                _currentRun = null;
                _currentRunCompletionState = RunCompletionState.None;
                _currentRunError = null;
                _currentRunStopRequested = false;
                _currentRunHistory = null;

                RemoveEntryIfInactive_NoLock(run.Flow.Id, entry);
            }

            if (runtimeError != null)
            {
                RuntimeError?.Invoke(this, runtimeError);
            }

            if (completedHistoryEntry != null)
            {
                await PersistExecutionHistoryAsync(completedHistoryEntry);
            }

            EmitRuntimeStatusChanged();
        }
    }

    private FlowQueuedEvent EnqueueRun(
        Flow flow,
        FlowRunSource source,
        string? triggerNodeId,
        Dictionary<string, object?>? triggerData)
    {
        EnsureFlowIdentity(flow);

        FlowQueuedEvent queueEvent;
        var coalesced = false;

        lock (_sync)
        {
            var entry = GetOrCreateEntry_NoLock(flow.Id, flow.Name);
            entry.Flow = flow;

            var existingPending = _queue.FirstOrDefault(item =>
                string.Equals(item.Flow.Id, flow.Id, StringComparison.OrdinalIgnoreCase));

            if (existingPending != null)
            {
                existingPending.Flow = flow;
                existingPending.Source = source;
                existingPending.TriggerNodeId = triggerNodeId;
                existingPending.TriggerData = triggerData is null
                    ? null
                    : new Dictionary<string, object?>(triggerData);
                existingPending.EnqueuedAt = DateTime.UtcNow;
                coalesced = true;
            }
            else
            {
                _queue.Enqueue(new QueuedFlowRun
                {
                    Flow = flow,
                    Source = source,
                    TriggerNodeId = triggerNodeId,
                    TriggerData = triggerData is null
                        ? null
                        : new Dictionary<string, object?>(triggerData),
                    EnqueuedAt = DateTime.UtcNow
                });
                _queueSignal.Release();
            }

            entry.QueuePending = true;

            queueEvent = new FlowQueuedEvent
            {
                FlowId = flow.Id,
                FlowName = flow.Name,
                Source = source,
                TriggerNodeId = triggerNodeId,
                QueueLength = _queue.Count,
                QueuePending = true
            };
        }

        if (coalesced)
        {
            FlowQueueCoalesced?.Invoke(this, queueEvent);
        }
        else
        {
            FlowQueued?.Invoke(this, queueEvent);
        }

        EmitRuntimeStatusChanged();
        return queueEvent;
    }

    private void OnNodeStatusChanged(string nodeId, NodeStatus status)
    {
        RecordCurrentRunNodeStatus(nodeId, status);
        NodeStatusChanged?.Invoke(nodeId, status);
    }

    private void OnExecutorLogMessage(string nodeId, string message)
    {
        RecordCurrentRunLog(nodeId, message);
        LogMessage?.Invoke(nodeId, message);
    }

    private void OnFlowCompleted(string flowId)
    {
        var shouldEmitCompleted = true;

        lock (_sync)
        {
            if (_currentRun != null && string.Equals(_currentRun.Flow.Id, flowId, StringComparison.OrdinalIgnoreCase))
            {
                if (_currentRunStopRequested)
                {
                    shouldEmitCompleted = false;
                    return;
                }

                _currentRunCompletionState = RunCompletionState.Completed;
            }
        }

        if (shouldEmitCompleted)
        {
            FlowCompleted?.Invoke(flowId);
        }
    }

    private void OnFlowError(string flowId, string error)
    {
        lock (_sync)
        {
            if (_currentRun != null && string.Equals(_currentRun.Flow.Id, flowId, StringComparison.OrdinalIgnoreCase))
            {
                _currentRunCompletionState = RunCompletionState.Error;
                _currentRunError = error;
            }
        }

        FlowError?.Invoke(flowId, error);
    }

    private void OnPhaseChanged(string nodeId, string phase, string? message, object? detail)
    {
        QueuedFlowRun? currentRun;
        lock (_sync)
        {
            currentRun = _currentRun;
            if (_currentRunHistory != null)
            {
                _currentRunHistory.Logs.Add(new ExecutionHistoryLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "phase",
                    NodeId = nodeId,
                    Message = string.IsNullOrWhiteSpace(message) ? phase : $"{phase}: {message}"
                });
            }
        }

        if (currentRun is null)
            return;

        RuntimePhaseChanged?.Invoke(this, new RuntimePhaseEvent
        {
            FlowId = currentRun.Flow.Id,
            FlowName = currentRun.Flow.Name,
            NodeId = nodeId,
            Phase = phase,
            Message = message,
            Detail = detail,
            Timestamp = DateTime.UtcNow
        });
    }

    private void OnTriggerFired(object? sender, TriggerFiredEventArgs e)
    {
        var flow = CloneFlow(e.Flow);
        EnsureFlowIdentity(flow);

        lock (_sync)
        {
            var entry = GetOrCreateEntry_NoLock(flow.Id, flow.Name);
            entry.Flow = flow;
            entry.LastTriggerAt = e.Timestamp;
        }

        TriggerFired?.Invoke(this, new TriggerRuntimeEvent
        {
            FlowId = flow.Id,
            FlowName = flow.Name,
            TriggerNodeId = e.TriggerNodeId,
            TriggeredAt = e.Timestamp
        });

        EnqueueRun(flow, FlowRunSource.Trigger, e.TriggerNodeId, e.Data);
    }

    private void OnTriggerManagerLogMessage(object? sender, TriggerManagerLogEventArgs e)
    {
        LogMessage?.Invoke(e.NodeId, e.Message);
    }

    private void OnTriggerManagerError(object? sender, TriggerManagerErrorEventArgs e)
    {
        Flow? flow = null;

        lock (_sync)
        {
            if (_flowStates.TryGetValue(e.FlowId, out var entry))
            {
                entry.LastError = e.Error;
                flow = entry.Flow;
            }
        }

        EmitRuntimeError(new RuntimeErrorEvent
        {
            FlowId = e.FlowId,
            FlowName = flow?.Name ?? "",
            Error = e.Error
        });
    }

    private void RecordFlowError(Flow flow, string error)
    {
        RuntimeErrorEvent runtimeError;

        lock (_sync)
        {
            var entry = GetOrCreateEntry_NoLock(flow.Id, flow.Name);
            entry.Flow = flow;
            entry.LastError = error;
            runtimeError = CreateRuntimeError_NoLock(entry, error);
        }

        EmitRuntimeError(runtimeError);
    }

    private void EmitRuntimeError(RuntimeErrorEvent runtimeError)
    {
        RuntimeError?.Invoke(this, runtimeError);
        EmitRuntimeStatusChanged();
    }

    private void EmitRuntimeStatusChanged()
    {
        RuntimeStatusSnapshot snapshot;
        lock (_sync)
        {
            snapshot = CreateRuntimeStatusSnapshot_NoLock();
        }

        RuntimeStatusChanged?.Invoke(this, snapshot);
    }

    private FlowExecutionHistoryEntry? CaptureCurrentRunHistoryEntry()
    {
        lock (_sync)
        {
            return CaptureCurrentRunHistoryEntry_NoLock();
        }
    }

    private FlowExecutionHistoryEntry? CaptureCurrentRunHistoryEntry_NoLock(DateTime? finishedAt = null)
    {
        if (_currentRunHistory == null)
        {
            return null;
        }

        var result = FlowExecutionResult.Running;
        var error = _currentRunError;
        if (_currentRunStopRequested)
        {
            result = FlowExecutionResult.Cancelled;
            error = null;
        }
        else if (_currentRunCompletionState == RunCompletionState.Error)
        {
            result = FlowExecutionResult.Error;
        }
        else if (_currentRunCompletionState == RunCompletionState.Completed)
        {
            result = FlowExecutionResult.Completed;
            error = null;
        }

        return new FlowExecutionHistoryEntry
        {
            RunId = _currentRunHistory.RunId,
            FlowId = _currentRunHistory.FlowId,
            FlowName = _currentRunHistory.FlowName,
            Source = _currentRunHistory.Source,
            TriggerNodeId = _currentRunHistory.TriggerNodeId,
            StartedAt = _currentRunHistory.StartedAt,
            FinishedAt = finishedAt,
            Result = result,
            Error = error,
            Logs = _currentRunHistory.Logs.ToArray(),
            NodeStatuses = _currentRunHistory.NodeStatuses.ToArray()
        };
    }

    private void RecordCurrentRunNodeStatus(string nodeId, NodeStatus status)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return;
        }

        lock (_sync)
        {
            if (_currentRunHistory == null)
            {
                return;
            }

            _currentRunHistory.NodeStatuses.Add(new ExecutionNodeStatusEntry
            {
                Timestamp = DateTime.UtcNow,
                NodeId = nodeId,
                Status = status
            });
        }
    }

    private void RecordCurrentRunLog(string? nodeId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (_sync)
        {
            if (_currentRunHistory == null)
            {
                return;
            }

            _currentRunHistory.Logs.Add(new ExecutionHistoryLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = InferLogLevel(message),
                NodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId,
                Message = message
            });
        }
    }

    private async Task PersistExecutionHistoryAsync(FlowExecutionHistoryEntry entry)
    {
        if (_executionHistoryStore == null)
        {
            return;
        }

        try
        {
            await _executionHistoryStore.UpsertAsync(entry, _shutdownCts.Token);
            if (entry.Result != FlowExecutionResult.Running)
            {
                ExecutionHistoryRecorded?.Invoke(this, entry);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore shutdown races while the app is closing.
        }
        catch (Exception ex)
        {
            EmitRuntimeError(new RuntimeErrorEvent
            {
                FlowId = entry.FlowId,
                FlowName = entry.FlowName,
                Error = $"Failed to persist execution history: {ex.Message}"
            });
        }
    }

    private static string InferLogLevel(string message)
    {
        if (message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("exception", StringComparison.OrdinalIgnoreCase))
        {
            return "error";
        }

        if (message.Contains("cancel", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("warn", StringComparison.OrdinalIgnoreCase))
        {
            return "warning";
        }

        return "info";
    }

    private void EnsureFlowIdentity(Flow flow)
    {
        if (string.IsNullOrWhiteSpace(flow.Id))
        {
            flow.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(flow.Name))
        {
            flow.Name = "Untitled Flow";
        }
    }

    private FlowRuntimeEntry GetOrCreateEntry_NoLock(string flowId, string flowName)
    {
        if (_flowStates.TryGetValue(flowId, out var existing))
        {
            if (!string.IsNullOrWhiteSpace(flowName))
            {
                existing.Flow.Name = flowName;
            }

            return existing;
        }

        var entry = new FlowRuntimeEntry
        {
            Flow = new Flow
            {
                Id = flowId,
                Name = string.IsNullOrWhiteSpace(flowName) ? "Untitled Flow" : flowName
            }
        };

        _flowStates[flowId] = entry;
        return entry;
    }

    private void RefreshPendingRuns_NoLock(string flowId, Flow flow)
    {
        foreach (var pending in _queue.Where(item =>
                     string.Equals(item.Flow.Id, flowId, StringComparison.OrdinalIgnoreCase)))
        {
            pending.Flow = CloneFlow(flow);
        }
    }

    private bool HasPendingRun_NoLock(string flowId)
    {
        return _queue.Any(item => string.Equals(item.Flow.Id, flowId, StringComparison.OrdinalIgnoreCase));
    }

    private void ClearQueuedRuns_NoLock()
    {
        while (_queue.Count > 0)
        {
            _queue.Dequeue();
        }

        foreach (var (flowId, entry) in _flowStates.ToArray())
        {
            entry.QueuePending = false;
            RemoveEntryIfInactive_NoLock(flowId, entry);
        }
    }

    private void RemoveEntryIfInactive_NoLock(string flowId, FlowRuntimeEntry entry)
    {
        if (entry.IsArmed || entry.IsRunning || entry.QueuePending || !string.IsNullOrWhiteSpace(entry.LastError))
        {
            return;
        }

        _flowStates.Remove(flowId);
    }

    private RuntimeStatusSnapshot CreateRuntimeStatusSnapshot_NoLock()
    {
        var snapshots = _flowStates.Values
            .Select(CreateFlowSnapshot_NoLock)
            .OrderByDescending(snapshot => snapshot.IsRunning)
            .ThenByDescending(snapshot => snapshot.QueuePending)
            .ThenByDescending(snapshot => snapshot.IsArmed)
            .ThenBy(snapshot => snapshot.FlowName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new RuntimeStatusSnapshot
        {
            IsRunning = _currentRun != null,
            QueueLength = _queue.Count,
            ArmedFlowCount = _flowStates.Values.Count(entry => entry.IsArmed),
            CurrentRun = _currentRun == null
                ? null
                : new CurrentRunSnapshot
                {
                    FlowId = _currentRun.Flow.Id,
                    FlowName = _currentRun.Flow.Name,
                    Source = _currentRun.Source,
                    TriggerNodeId = _currentRun.TriggerNodeId,
                    StartedAt = _currentRun.StartedAt ?? DateTime.UtcNow
                },
            Flows = snapshots
        };
    }

    private FlowRuntimeSnapshot CreateFlowSnapshot_NoLock(FlowRuntimeEntry entry)
    {
        return new FlowRuntimeSnapshot
        {
            FlowId = entry.Flow.Id,
            FlowName = entry.Flow.Name,
            State = ResolveState(entry),
            IsArmed = entry.IsArmed,
            IsRunning = entry.IsRunning,
            QueuePending = entry.QueuePending,
            ActiveTriggerNodeIds = entry.ActiveTriggerNodeIds.ToArray(),
            LastTriggerAt = entry.LastTriggerAt,
            LastRunAt = entry.LastRunAt,
            LastError = entry.LastError
        };
    }

    private RuntimeErrorEvent CreateRuntimeError_NoLock(FlowRuntimeEntry entry, string error)
    {
        return new RuntimeErrorEvent
        {
            FlowId = entry.Flow.Id,
            FlowName = entry.Flow.Name,
            Error = error
        };
    }

    private static FlowRuntimeState ResolveState(FlowRuntimeEntry entry)
    {
        if (entry.IsRunning)
        {
            return FlowRuntimeState.Running;
        }

        if (entry.QueuePending)
        {
            return FlowRuntimeState.Queued;
        }

        if (!string.IsNullOrWhiteSpace(entry.LastError))
        {
            return FlowRuntimeState.Error;
        }

        if (entry.IsArmed)
        {
            return FlowRuntimeState.Armed;
        }

        return FlowRuntimeState.Inactive;
    }

    private static Flow CloneFlow(Flow flow)
    {
        var clone = FlowSerializer.Deserialize(FlowSerializer.Serialize(flow));
        return clone ?? throw new InvalidOperationException("Failed to clone flow for runtime execution.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(FlowRuntimeManager));
        }
    }

    private sealed class FlowRuntimeEntry
    {
        public Flow Flow { get; set; } = new();
        public bool IsArmed { get; set; }
        public bool IsRunning { get; set; }
        public bool QueuePending { get; set; }
        public string[] ActiveTriggerNodeIds { get; set; } = [];
        public DateTime? LastTriggerAt { get; set; }
        public DateTime? LastRunAt { get; set; }
        public string? LastError { get; set; }
    }

    private sealed class QueuedFlowRun
    {
        public Flow Flow { get; set; } = new();
        public FlowRunSource Source { get; set; }
        public string? TriggerNodeId { get; set; }
        public Dictionary<string, object?>? TriggerData { get; set; }
        public DateTime EnqueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
    }

    private sealed class ActiveRunHistory
    {
        public string RunId { get; init; } = "";
        public string FlowId { get; init; } = "";
        public string FlowName { get; init; } = "";
        public FlowRunSource Source { get; init; }
        public string? TriggerNodeId { get; init; }
        public DateTime StartedAt { get; init; }
        public List<ExecutionHistoryLogEntry> Logs { get; } = [];
        public List<ExecutionNodeStatusEntry> NodeStatuses { get; } = [];
    }

    private enum RunCompletionState
    {
        None,
        Completed,
        Error
    }
}
