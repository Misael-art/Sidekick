import { useMemo } from 'react';
import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';
import type { FlowExecutionHistoryEntry, FlowRuntimeSnapshot } from '../../bridge/types';

function formatTime(value?: string | null): string {
  if (!value) {
    return 'Never';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleTimeString('pt-BR');
}

function formatRelativeState(flow: FlowRuntimeSnapshot | null, fallback: string): string {
  if (!flow) {
    return fallback;
  }

  switch (flow.state) {
    case 'running':
      return 'Running';
    case 'queued':
      return 'Queued';
    case 'armed':
      return 'Armed';
    case 'error':
      return 'Error';
    default:
      return 'Inactive';
  }
}

function formatHistoryResult(entry: FlowExecutionHistoryEntry): string {
  switch (entry.result) {
    case 'completed':
      return 'Completed';
    case 'error':
      return 'Failed';
    case 'cancelled':
      return 'Cancelled';
    default:
      return 'Running';
  }
}

export default function ExecutionStatus() {
  const runtimeStatus = useAppStore((s) => s.runtimeStatus);
  const currentRun = useAppStore((s) => s.currentRun);
  const flowRuntimes = useAppStore((s) => s.flowRuntimes);
  const logs = useAppStore((s) => s.logs);
  const executionHistory = useAppStore((s) => s.executionHistory);
  const clearLogs = useAppStore((s) => s.clearLogs);
  const isLogsExpanded = useAppStore((s) => s.isLogsExpanded);
  const toggleLogsExpanded = useAppStore((s) => s.toggleLogsExpanded);
  const userMessage = useAppStore((s) => s.userMessage);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const flowId = useFlowStore((s) => s.flowId);
  const flowName = useFlowStore((s) => s.flowName);
  const validationResult = useFlowStore((s) => s.validationResult);

  const currentEditorRuntime = flowRuntimes[flowId] ?? null;
  const armedFlows = useMemo(
    () => runtimeStatus.flows.filter((flow) => flow.isArmed),
    [runtimeStatus.flows],
  );
  const errorCount = logs.filter((entry) => entry.level === 'error').length;
  const warningCount = logs.filter((entry) => entry.level === 'warning').length;
  const validationErrorCount = validationResult?.errors.length ?? 0;
  const validationWarningCount = validationResult?.warnings.length ?? 0;
  const validationSummary = validationResult == null
    ? 'Not run'
    : validationResult.isValid
      ? (validationWarningCount > 0 ? 'Valid with warnings' : 'Valid')
      : 'Blocked by errors';
  const recentFlowRuns = useMemo(
    () => executionHistory
      .filter((entry) => !flowId || entry.flowId === flowId)
      .slice(0, 3),
    [executionHistory, flowId],
  );
  const latestRun = recentFlowRuns[0] ?? null;

  return (
    <div className={`exec-status ${isLogsExpanded ? 'exec-status--expanded' : ''}`}>
      <div className="exec-status__bar">
        <div className="exec-status__left">
          <span className="exec-status__flow-name">{flowName}</span>
          <span className={`exec-status__indicator ${runtimeStatus.isRunning ? 'exec-status__indicator--running' : 'exec-status__indicator--stopped'}`} />
          <span className="exec-status__state">{formatRelativeState(currentEditorRuntime, 'Inactive')}</span>
        </div>

        <div className="exec-status__center exec-status__center--runtime">
          <span className="exec-status__metric">
            <strong>{runtimeStatus.armedFlowCount}</strong>
            {' '}
            armed
          </span>
          <span className="exec-status__metric">
            <strong>{runtimeStatus.queueLength}</strong>
            {' '}
            queued
          </span>
          <span className="exec-status__metric">
            Current:
            {' '}
            <strong>{currentRun?.flowName ?? 'Idle'}</strong>
          </span>
          {currentRun?.source && (
            <span className="exec-status__metric exec-status__metric--subtle">
              via
              {' '}
              {currentRun.source}
            </span>
          )}
        </div>

        <div className="exec-status__right">
          {errorCount > 0 && (
            <span className="exec-status__badge exec-status__badge--error">
              {errorCount} error{errorCount !== 1 ? 's' : ''}
            </span>
          )}
          {warningCount > 0 && (
            <span className="exec-status__badge exec-status__badge--warning">
              {warningCount} warning{warningCount !== 1 ? 's' : ''}
            </span>
          )}
          {validationErrorCount > 0 && (
            <span className="exec-status__badge exec-status__badge--error">
              {validationErrorCount} validation error{validationErrorCount !== 1 ? 's' : ''}
            </span>
          )}
          {validationWarningCount > 0 && (
            <span className="exec-status__badge exec-status__badge--warning">
              {validationWarningCount} validation warning{validationWarningCount !== 1 ? 's' : ''}
            </span>
          )}
          <span className="exec-status__log-count">
            {logs.length} log{logs.length !== 1 ? 's' : ''}
          </span>
          <button
            className="exec-status__expand-btn"
            onClick={toggleLogsExpanded}
            title={isLogsExpanded ? 'Collapse logs' : 'Expand logs'}
          >
            {isLogsExpanded ? '\u25BC' : '\u25B2'}
          </button>
        </div>
      </div>

      {userMessage && (
        <div className={`exec-status__message exec-status__message--${userMessage.type}`}>
          <span>{userMessage.text}</span>
          <button
            className="exec-status__message-close"
            onClick={() => setUserMessage(null)}
            title="Dismiss message"
          >
            x
          </button>
        </div>
      )}

      <div className="exec-status__summary">
        <div className="exec-status__summary-card">
          <span className="exec-status__summary-label">Editor flow</span>
          <strong>{formatRelativeState(currentEditorRuntime, 'Inactive')}</strong>
          <span className="exec-status__summary-meta">
            Last run: {formatTime(currentEditorRuntime?.lastRunAt)}
          </span>
          <span className="exec-status__summary-meta">
            Last trigger: {formatTime(currentEditorRuntime?.lastTriggerAt)}
          </span>
        </div>

        <div className="exec-status__summary-card">
          <span className="exec-status__summary-label">Runtime focus</span>
          <strong>{currentRun?.flowName ?? 'Idle'}</strong>
          <span className="exec-status__summary-meta">
            Queue depth: {runtimeStatus.queueLength}
          </span>
          <span className="exec-status__summary-meta">
            Armed flows: {runtimeStatus.armedFlowCount}
          </span>
        </div>

        <div className="exec-status__summary-card">
          <span className="exec-status__summary-label">Errors</span>
          <strong>{currentEditorRuntime?.lastError ?? 'None'}</strong>
          <span className="exec-status__summary-meta">
            Flow id: {flowId}
          </span>
        </div>

        <div className="exec-status__summary-card">
          <span className="exec-status__summary-label">Validation</span>
          <strong>{validationSummary}</strong>
          <span className="exec-status__summary-meta">
            Errors: {validationErrorCount}
          </span>
          <span className="exec-status__summary-meta">
            Warnings: {validationWarningCount}
          </span>
        </div>

        <div className="exec-status__summary-card">
          <span className="exec-status__summary-label">Recent run</span>
          <strong>{latestRun ? formatHistoryResult(latestRun) : 'No history yet'}</strong>
          <span className="exec-status__summary-meta">
            Started: {formatTime(latestRun?.startedAt)}
          </span>
          <span className="exec-status__summary-meta">
            Source: {latestRun?.source ?? 'n/a'}
          </span>
        </div>
      </div>

      {isLogsExpanded && (
        <div className="exec-status__logs">
          <div className="exec-status__logs-toolbar">
            <div className="exec-status__armed-list">
              {armedFlows.length === 0 ? (
                <span className="exec-status__armed-empty">Nenhum flow armado no momento.</span>
              ) : (
                armedFlows.map((flow) => (
                  <div key={flow.flowId} className="exec-status__armed-item">
                    <strong>{flow.flowName}</strong>
                    <span>{formatRelativeState(flow, 'Inactive')}</span>
                    <span>{flow.activeTriggerNodeIds.length} trigger(s)</span>
                  </div>
                ))
              )}
            </div>
            <button className="exec-status__clear-btn" onClick={clearLogs}>
              Clear
            </button>
          </div>
          {recentFlowRuns.length > 0 && (
            <div className="exec-status__history">
              {recentFlowRuns.map((entry) => (
                <div key={entry.runId} className={`exec-status__history-item exec-status__history-item--${entry.result}`}>
                  <strong>{entry.flowName}</strong>
                  <span>{formatHistoryResult(entry)}</span>
                  <span>{formatTime(entry.startedAt)}</span>
                  <span>{entry.logs.length} log{entry.logs.length !== 1 ? 's' : ''}</span>
                </div>
              ))}
            </div>
          )}
          <div className="exec-status__logs-list">
            {validationResult && validationResult.issues.length > 0 && (
              <>
                {validationResult.issues.map((issue, index) => (
                  <div
                    key={`validation-${issue.code}-${issue.nodeId ?? 'global'}-${index}`}
                    className={`exec-status__log exec-status__log--${issue.severity}`}
                  >
                    <span className="exec-status__log-time">VALIDATE</span>
                    <span className="exec-status__log-level">[{issue.severity}]</span>
                    <span className="exec-status__log-msg">{issue.message}</span>
                  </div>
                ))}
              </>
            )}
            {logs.length === 0 && (
              <div className="exec-status__logs-empty">No log entries</div>
            )}
            {logs.map((entry, index) => (
              <div
                key={`${entry.timestamp}-${index}`}
                className={`exec-status__log exec-status__log--${entry.level}`}
              >
                <span className="exec-status__log-time">
                  {entry.timestamp.slice(11, 19)}
                </span>
                <span className="exec-status__log-level">[{entry.level}]</span>
                <span className="exec-status__log-msg">{entry.message}</span>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
