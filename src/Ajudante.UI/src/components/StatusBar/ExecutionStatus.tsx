import { useMemo } from 'react';
import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';
import type { FlowExecutionHistoryEntry, FlowRuntimeSnapshot, RuntimePhaseEvent } from '../../bridge/types';

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

function getDetailString(detail: unknown, keys: string[]): string {
  if (!detail || typeof detail !== 'object') {
    return '';
  }

  const source = detail as Record<string, unknown>;
  for (const key of keys) {
    const value = source[key];
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
    if (typeof value === 'number' || typeof value === 'boolean') {
      return String(value);
    }
  }

  return '';
}

function buildPhaseInsight(
  phase: RuntimePhaseEvent | null,
  latestStatus: { nodeId: string; status: string } | null,
  currentRunName: string,
) {
  const nodeId = phase?.nodeId || latestStatus?.nodeId || 'aguardando';
  const status = latestStatus?.status ?? 'Idle';
  const expected = getDetailString(phase?.detail, ['expected', 'selector', 'action', 'target', 'message'])
    || phase?.message
    || (status === 'Running' ? 'Executando o passo atual.' : 'Aguardando o proximo evento do runtime.');
  const fallback = getDetailString(phase?.detail, ['fallback', 'fallbackActive', 'fallbackReason']);
  const errorOrWait = getDetailString(phase?.detail, ['error', 'wait', 'waitingFor', 'timeout']);
  const nextStep = getDetailString(phase?.detail, ['nextStep', 'suggestion', 'recommendation'])
    || (fallback || errorOrWait ? 'Revise o seletor, rode Dry-run e repare com Mira se necessario.' : 'Acompanhe a trilha ativa no canvas.');

  return {
    flowName: phase?.flowName || currentRunName,
    nodeId,
    phase: phase?.phase || status,
    expected,
    fallback,
    errorOrWait,
    nextStep,
  };
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
  const debugVisualEnabled = useAppStore((s) => s.debugVisualEnabled);
  const nodeStatusTimeline = useAppStore((s) => s.nodeStatusTimeline);
  const runtimePhases = useAppStore((s) => s.runtimePhases);
  const flowHealthReport = useAppStore((s) => s.flowHealthReport);

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
  const timelineEntries = useMemo(
    () => nodeStatusTimeline
      .filter(() => !flowId || currentRun?.flowId === flowId || latestRun?.flowId === flowId)
      .slice(-8)
      .reverse(),
    [currentRun?.flowId, flowId, latestRun?.flowId, nodeStatusTimeline],
  );
  const latestDebugEntry = timelineEntries[0] ?? null;
  const latestRuntimePhase = useMemo(
    () => runtimePhases
      .filter((phase) => !flowId || phase.flowId === flowId || currentRun?.flowId === phase.flowId)
      .at(-1) ?? null,
    [currentRun?.flowId, flowId, runtimePhases],
  );
  const debugInsight = buildPhaseInsight(
    latestRuntimePhase,
    latestDebugEntry,
    currentRun?.flowName ?? flowName,
  );
  const hasRuntimeContext = runtimeStatus.isRunning
    || runtimeStatus.queueLength > 0
    || Boolean(currentRun)
    || Boolean(currentEditorRuntime?.lastError)
    || debugVisualEnabled;
  const shouldShowSummary = isLogsExpanded || hasRuntimeContext;
  const actionableNextStep = currentEditorRuntime?.lastError
    ? 'Abra os logs, ajuste o passo com erro e rode Dry-run.'
    : runtimeStatus.isRunning
      ? debugInsight.nextStep
      : flowHealthReport && flowHealthReport.issues.length > 0
        ? 'Corrija o primeiro item de Saude antes de executar.'
        : 'Grave uma automacao, use Receitas ou pressione / para adicionar um passo.';

  return (
    <div className={`exec-status ${isLogsExpanded ? 'exec-status--expanded' : ''}`}>
      {debugVisualEnabled && (currentRun || latestDebugEntry) && (
        <div className="exec-status__debug-overlay" role="status" aria-live="polite">
          <strong>Debug pedagogico</strong>
          <span><b>Flow</b> {debugInsight.flowName}</span>
          <span><b>Node atual</b> {debugInsight.nodeId}</span>
          <span><b>Fase</b> {debugInsight.phase}</span>
          <span><b>Esperado</b> {debugInsight.expected}</span>
          {debugInsight.fallback && <span><b>Fallback ativo</b> {debugInsight.fallback}</span>}
          {debugInsight.errorOrWait && <span><b>Espera/erro</b> {debugInsight.errorOrWait}</span>}
          <span><b>Proximo passo</b> {debugInsight.nextStep}</span>
        </div>
      )}

      <div className="exec-status__bar">
        <div className="exec-status__left">
          <span className="exec-status__flow-name">{flowName}</span>
          <span className={`exec-status__indicator ${runtimeStatus.isRunning ? 'exec-status__indicator--running' : 'exec-status__indicator--stopped'}`} />
          <span className="exec-status__state">{formatRelativeState(currentEditorRuntime, 'Inactive')}</span>
        </div>

        <div className="exec-status__center exec-status__center--runtime">
          <span className="exec-status__metric">
            Monitorando:
            {' '}
            <strong>{runtimeStatus.armedFlowCount}</strong>
          </span>
          <span className="exec-status__metric">
            Fila:
            {' '}
            <strong>{runtimeStatus.queueLength}</strong>
          </span>
          <span className="exec-status__metric">
            Atual:
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
          <span className="exec-status__metric exec-status__metric--action">
            Proxima acao:
            {' '}
            <strong>{actionableNextStep}</strong>
          </span>
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
        <div
          className={`exec-status__message exec-status__message--${userMessage.type}`}
          role={userMessage.type === 'error' ? 'alert' : 'status'}
          aria-live={userMessage.type === 'error' ? 'assertive' : 'polite'}
        >
          <span>{userMessage.text}</span>
          <button
            className="exec-status__message-close"
            onClick={() => setUserMessage(null)}
            title="Fechar mensagem"
            aria-label="Fechar mensagem"
          >
            x
          </button>
        </div>
      )}

      {shouldShowSummary && (
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
          <span className="exec-status__summary-label">Flow Health</span>
          <strong>{flowHealthReport ? `${flowHealthReport.score}/100` : 'Not run'}</strong>
          <span className="exec-status__summary-meta">
            Level: {flowHealthReport?.level ?? 'n/a'}
          </span>
          <span className="exec-status__summary-meta">
            Issues: {flowHealthReport?.issues.length ?? 0}
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
      )}

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
          {debugVisualEnabled && timelineEntries.length > 0 && (
            <div className="exec-status__history">
              {timelineEntries.map((entry) => (
                <div key={`${entry.at}-${entry.nodeId}`} className="exec-status__history-item">
                  <strong>{entry.nodeId}</strong>
                  <span>{entry.status}</span>
                  <span>{formatTime(entry.at)}</span>
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
