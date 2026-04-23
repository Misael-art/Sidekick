import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';
import { sendCommand } from '../../bridge/bridge';
import { toBackendFlow } from '../../bridge/flowConverter';

function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

export default function ExecutionStatus() {
  const isRunning = useAppStore((s) => s.isRunning);
  const setRunning = useAppStore((s) => s.setRunning);
  const runningFlowId = useAppStore((s) => s.runningFlowId);
  const runningFlowName = useAppStore((s) => s.runningFlowName);
  const setRunningFlow = useAppStore((s) => s.setRunningFlow);
  const clearRunningFlow = useAppStore((s) => s.clearRunningFlow);
  const logs = useAppStore((s) => s.logs);
  const clearLogs = useAppStore((s) => s.clearLogs);
  const isLogsExpanded = useAppStore((s) => s.isLogsExpanded);
  const toggleLogsExpanded = useAppStore((s) => s.toggleLogsExpanded);
  const userMessage = useAppStore((s) => s.userMessage);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const addLog = useAppStore((s) => s.addLog);
  const clearNodeStatuses = useAppStore((s) => s.clearNodeStatuses);
  const flowId = useFlowStore((s) => s.flowId);
  const flowName = useFlowStore((s) => s.flowName);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);

  const errorCount = logs.filter((l) => l.level === 'error').length;
  const warningCount = logs.filter((l) => l.level === 'warning').length;
  const activeFlowName = isRunning ? (runningFlowName || flowName) : flowName;
  const isShowingRestoredFlow = isRunning && !!runningFlowName && runningFlowName !== flowName;

  const handlePlay = async () => {
    try {
      setUserMessage(null);
      setRunning(true);
      setRunningFlow(flowId || null, flowName);
      clearNodeStatuses();
      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges);
      await sendCommand('engine', 'runFlow', backendFlow);
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel executar o fluxo.');
      setRunning(false);
      clearRunningFlow();
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleStop = async () => {
    try {
      setUserMessage(null);
      await sendCommand('engine', 'stopFlow', {});
      setRunning(false);
      clearRunningFlow();
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel interromper o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const levelClass = (level: string) => {
    switch (level) {
      case 'error':
        return 'exec-status__log--error';
      case 'warning':
        return 'exec-status__log--warning';
      case 'debug':
        return 'exec-status__log--debug';
      default:
        return '';
    }
  };

  return (
    <div className={`exec-status ${isLogsExpanded ? 'exec-status--expanded' : ''}`}>
      <div className="exec-status__bar">
        <div className="exec-status__left">
          <span className="exec-status__flow-name">
            {activeFlowName}
            {isShowingRestoredFlow && runningFlowId ? ` (${runningFlowId})` : ''}
          </span>
          <span
            className={`exec-status__indicator ${isRunning ? 'exec-status__indicator--running' : 'exec-status__indicator--stopped'}`}
          />
          <span className="exec-status__state">
            {isRunning ? 'Running' : 'Stopped'}
          </span>
        </div>

        <div className="exec-status__center">
          <button
            className="exec-status__btn exec-status__btn--play"
            onClick={handlePlay}
            disabled={isRunning}
            title="Run flow"
          >
            &#9654;
          </button>
          <button
            className="exec-status__btn exec-status__btn--stop"
            onClick={handleStop}
            disabled={!isRunning}
            title="Stop flow"
          >
            &#9632;
          </button>
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

      {isLogsExpanded && (
        <div className="exec-status__logs">
          <div className="exec-status__logs-toolbar">
            <button className="exec-status__clear-btn" onClick={clearLogs}>
              Clear
            </button>
          </div>
          <div className="exec-status__logs-list">
            {logs.length === 0 && (
              <div className="exec-status__logs-empty">No log entries</div>
            )}
            {logs.map((entry, idx) => (
              <div
                key={idx}
                className={`exec-status__log ${levelClass(entry.level)}`}
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
