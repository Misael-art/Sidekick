import { useState, useRef, useEffect, type KeyboardEvent } from 'react';
import { useFlowStore } from '../../store/flowStore';
import { useAppStore } from '../../store/appStore';
import { sendCommand } from '../../bridge/bridge';
import { toBackendFlow } from '../../bridge/flowConverter';

export default function Toolbar() {
  const flowName = useFlowStore((s) => s.flowName);
  const flowId = useFlowStore((s) => s.flowId);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const setFlowName = useFlowStore((s) => s.setFlowName);
  const saveFlow = useFlowStore((s) => s.saveFlow);
  const newFlow = useFlowStore((s) => s.newFlow);
  const isRunning = useAppStore((s) => s.isRunning);
  const setRunning = useAppStore((s) => s.setRunning);
  const clearNodeStatuses = useAppStore((s) => s.clearNodeStatuses);
  const inspectorMode = useAppStore((s) => s.inspectorMode);
  const setInspectorMode = useAppStore((s) => s.setInspectorMode);

  const [isEditing, setIsEditing] = useState(false);
  const [editName, setEditName] = useState(flowName);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isEditing && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [isEditing]);

  const commitName = () => {
    const trimmed = editName.trim();
    if (trimmed) {
      setFlowName(trimmed);
    }
    setIsEditing(false);
  };

  const handleNameKeyDown = (e: KeyboardEvent) => {
    if (e.key === 'Enter') commitName();
    if (e.key === 'Escape') {
      setEditName(flowName);
      setIsEditing(false);
    }
  };

  const handlePlay = async () => {
    setRunning(true);
    clearNodeStatuses();
    const backendFlow = toBackendFlow(flowId, flowName, nodes, edges);
    await sendCommand('engine', 'runFlow', backendFlow);
  };

  const handleStop = async () => {
    await sendCommand('engine', 'stopFlow', {});
    setRunning(false);
  };

  const handleNew = () => {
    if (isRunning) return;
    newFlow();
  };

  const handleSave = async () => {
    await saveFlow();
  };

  const handleLoad = async () => {
    const flows = await sendCommand('flow', 'listFlows', {});
    console.log('[Toolbar] Available flows:', flows);
    // TODO: show a proper flow picker UI; for now just log the list
  };

  const toggleMira = () => {
    if (inspectorMode === 'mira') {
      sendCommand('platform', 'cancelInspector', {});
      setInspectorMode('none');
    } else {
      sendCommand('platform', 'startMira', {});
      setInspectorMode('mira');
    }
  };

  const toggleSnip = () => {
    if (inspectorMode === 'snip') {
      sendCommand('platform', 'cancelInspector', {});
      setInspectorMode('none');
    } else {
      sendCommand('platform', 'startSnip', {});
      setInspectorMode('snip');
    }
  };

  return (
    <div className="toolbar">
      {/* Left: File operations */}
      <div className="toolbar__group">
        <button className="toolbar__btn" onClick={handleNew} title="New Flow">
          <span className="toolbar__btn-icon">&#x1F4C4;</span>
          <span className="toolbar__btn-label">New</span>
        </button>
        <button className="toolbar__btn" onClick={handleSave} title="Save Flow">
          <span className="toolbar__btn-icon">&#x1F4BE;</span>
          <span className="toolbar__btn-label">Save</span>
        </button>
        <button className="toolbar__btn" onClick={handleLoad} title="Load Flow">
          <span className="toolbar__btn-icon">&#x1F4C2;</span>
          <span className="toolbar__btn-label">Load</span>
        </button>
      </div>

      <div className="toolbar__divider" />

      {/* Center: Flow name */}
      <div className="toolbar__center">
        {isEditing ? (
          <input
            ref={inputRef}
            className="toolbar__name-input"
            value={editName}
            onChange={(e) => setEditName(e.target.value)}
            onBlur={commitName}
            onKeyDown={handleNameKeyDown}
          />
        ) : (
          <button
            className="toolbar__name"
            onClick={() => {
              setEditName(flowName);
              setIsEditing(true);
            }}
            title="Click to rename"
          >
            {flowName}
          </button>
        )}
      </div>

      <div className="toolbar__divider" />

      {/* Right: Execution and inspector */}
      <div className="toolbar__group">
        <button
          className={`toolbar__btn toolbar__btn--play ${isRunning ? 'toolbar__btn--disabled' : ''}`}
          onClick={handlePlay}
          disabled={isRunning}
          title="Run Flow"
        >
          <span className="toolbar__btn-icon">&#9654;</span>
          <span className="toolbar__btn-label">Play</span>
        </button>
        <button
          className={`toolbar__btn toolbar__btn--stop ${!isRunning ? 'toolbar__btn--disabled' : ''}`}
          onClick={handleStop}
          disabled={!isRunning}
          title="Stop Flow"
        >
          <span className="toolbar__btn-icon">&#9632;</span>
          <span className="toolbar__btn-label">Stop</span>
        </button>
      </div>

      <div className="toolbar__divider" />

      <div className="toolbar__group">
        <button
          className={`toolbar__btn ${inspectorMode === 'mira' ? 'toolbar__btn--active' : ''}`}
          onClick={toggleMira}
          title="Mira Inspector - Element detection"
        >
          <span className="toolbar__btn-icon">&#x1F441;</span>
          <span className="toolbar__btn-label">Mira</span>
        </button>
        <button
          className={`toolbar__btn ${inspectorMode === 'snip' ? 'toolbar__btn--active' : ''}`}
          onClick={toggleSnip}
          title="Snip - Screen region capture"
        >
          <span className="toolbar__btn-icon">&#x2702;</span>
          <span className="toolbar__btn-label">Snip</span>
        </button>
      </div>
    </div>
  );
}
