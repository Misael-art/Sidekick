import { useState, useRef, useEffect, useMemo, type KeyboardEvent } from 'react';
import { useFlowStore } from '../../store/flowStore';
import { useAppStore } from '../../store/appStore';
import { sendCommand } from '../../bridge/bridge';
import { toBackendFlow } from '../../bridge/flowConverter';

function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

interface FlowSummary {
  id: string;
  name?: string;
  modifiedAt?: string;
  nodeCount?: number;
}

function formatModifiedAt(value?: string): string {
  if (!value) {
    return 'Data indisponivel';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString('pt-BR');
}

function confirmDiscardUnsavedChanges(nextActionLabel: string): boolean {
  return window.confirm(
    `Existem alteracoes nao salvas no fluxo atual. Deseja descartalas e ${nextActionLabel}?`,
  );
}

function normalizeSearchText(value?: string): string {
  return value?.trim().toLocaleLowerCase('pt-BR') ?? '';
}

function isDemoFlow(flow: FlowSummary): boolean {
  const haystack = `${flow.id} ${flow.name ?? ''}`.toLocaleLowerCase('pt-BR');
  return haystack.includes('demo');
}

export default function Toolbar() {
  const flowName = useFlowStore((s) => s.flowName);
  const flowId = useFlowStore((s) => s.flowId);
  const isDirty = useFlowStore((s) => s.isDirty);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const setFlowName = useFlowStore((s) => s.setFlowName);
  const saveFlow = useFlowStore((s) => s.saveFlow);
  const newFlow = useFlowStore((s) => s.newFlow);
  const loadFlow = useFlowStore((s) => s.loadFlow);
  const isRunning = useAppStore((s) => s.isRunning);
  const setRunning = useAppStore((s) => s.setRunning);
  const setRunningFlow = useAppStore((s) => s.setRunningFlow);
  const clearRunningFlow = useAppStore((s) => s.clearRunningFlow);
  const clearNodeStatuses = useAppStore((s) => s.clearNodeStatuses);
  const inspectorMode = useAppStore((s) => s.inspectorMode);
  const setInspectorMode = useAppStore((s) => s.setInspectorMode);
  const addLog = useAppStore((s) => s.addLog);
  const setUserMessage = useAppStore((s) => s.setUserMessage);

  const [isEditing, setIsEditing] = useState(false);
  const [editName, setEditName] = useState(flowName);
  const [availableFlows, setAvailableFlows] = useState<FlowSummary[]>([]);
  const [isLoadDialogOpen, setIsLoadDialogOpen] = useState(false);
  const [selectedFlowId, setSelectedFlowId] = useState<string | null>(null);
  const [loadFilter, setLoadFilter] = useState('');
  const [isLoadingFlowList, setIsLoadingFlowList] = useState(false);
  const [isApplyingLoadedFlow, setIsApplyingLoadedFlow] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const loadSearchInputRef = useRef<HTMLInputElement>(null);

  const filteredFlows = useMemo(() => {
    const normalizedFilter = normalizeSearchText(loadFilter);
    if (!normalizedFilter) {
      return availableFlows;
    }

    return availableFlows.filter((flow) => {
      const normalizedName = normalizeSearchText(flow.name);
      const normalizedId = normalizeSearchText(flow.id);
      return normalizedName.includes(normalizedFilter) || normalizedId.includes(normalizedFilter);
    });
  }, [availableFlows, loadFilter]);

  useEffect(() => {
    if (isEditing && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [isEditing]);

  useEffect(() => {
    if (!isLoadDialogOpen) {
      return;
    }

    loadSearchInputRef.current?.focus();
    loadSearchInputRef.current?.select();

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && !isApplyingLoadedFlow) {
        setIsLoadDialogOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown as unknown as EventListener);
    return () => {
      window.removeEventListener('keydown', handleKeyDown as unknown as EventListener);
    };
  }, [isLoadDialogOpen, isApplyingLoadedFlow]);

  useEffect(() => {
    if (!isLoadDialogOpen) {
      return;
    }

    if (filteredFlows.length === 0) {
      setSelectedFlowId(null);
      return;
    }

    if (!selectedFlowId || !filteredFlows.some((flow) => flow.id === selectedFlowId)) {
      setSelectedFlowId(filteredFlows[0].id);
    }
  }, [filteredFlows, isLoadDialogOpen, selectedFlowId]);

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

  const handleNew = async () => {
    if (isRunning) return;
    if (isDirty && !confirmDiscardUnsavedChanges('criar um novo fluxo')) return;
    try {
      setUserMessage(null);
      await newFlow();
      setUserMessage({ type: 'success', text: 'Novo fluxo criado com sucesso.' });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: 'Novo fluxo criado com sucesso.',
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel criar um novo fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleSave = async () => {
    try {
      setUserMessage(null);
      await saveFlow();
      setUserMessage({ type: 'success', text: 'Fluxo salvo com sucesso.' });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Fluxo "${flowName}" salvo com sucesso.`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel salvar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleLoad = async () => {
    if (isRunning) return;

    try {
      setUserMessage(null);
      setIsLoadingFlowList(true);
      const flows = await sendCommand<FlowSummary[]>('flow', 'listFlows', {});
      const normalizedFlows = Array.isArray(flows) ? flows : [];
      setAvailableFlows(normalizedFlows);
      setLoadFilter('');
      setSelectedFlowId(normalizedFlows[0]?.id ?? null);
      setIsLoadDialogOpen(true);
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel listar os fluxos.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsLoadingFlowList(false);
    }
  };

  const handleSelectFlow = async (targetFlowId = selectedFlowId) => {
    if (!targetFlowId) {
      setUserMessage({ type: 'info', text: 'Selecione um fluxo para carregar.' });
      return;
    }

    if (isDirty && !confirmDiscardUnsavedChanges('carregar outro fluxo')) {
      return;
    }

    try {
      setIsApplyingLoadedFlow(true);
      setUserMessage(null);
      await loadFlow(targetFlowId);
      const selectedFlow = availableFlows.find((flow) => flow.id === targetFlowId);
      const loadedFlowName = selectedFlow?.name?.trim() || 'Fluxo';
      setIsLoadDialogOpen(false);
      setUserMessage({ type: 'success', text: `Fluxo "${loadedFlowName}" carregado com sucesso.` });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Fluxo "${loadedFlowName}" carregado com sucesso.`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel carregar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsApplyingLoadedFlow(false);
    }
  };

  const toggleMira = () => {
    if (inspectorMode === 'mira') {
      void sendCommand('platform', 'cancelInspector', {});
      setInspectorMode('none');
    } else {
      void sendCommand('platform', 'startMira', {});
      setInspectorMode('mira');
    }
  };

  const toggleSnip = () => {
    if (inspectorMode === 'snip') {
      void sendCommand('platform', 'cancelInspector', {});
      setInspectorMode('none');
    } else {
      void sendCommand('platform', 'startSnip', {});
      setInspectorMode('snip');
    }
  };

  return (
    <>
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
          <button
            className={`toolbar__btn ${isLoadingFlowList || isRunning ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleLoad}
            title="Load Flow"
            disabled={isLoadingFlowList || isRunning}
          >
            <span className="toolbar__btn-icon">&#x1F4C2;</span>
            <span className="toolbar__btn-label">{isLoadingFlowList ? 'Loading...' : 'Load'}</span>
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
              {flowName}{isDirty ? ' *' : ''}
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

      {isLoadDialogOpen && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => {
            if (!isApplyingLoadedFlow) {
              setIsLoadDialogOpen(false);
            }
          }}
        >
          <div
            className="toolbar__dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="load-flow-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="load-flow-dialog-title" className="toolbar__dialog-title">Carregar fluxo</h2>
                <p className="toolbar__dialog-subtitle">Escolha um fluxo salvo para abrir no editor.</p>
              </div>
              <button
                className="toolbar__dialog-close"
                onClick={() => setIsLoadDialogOpen(false)}
                disabled={isApplyingLoadedFlow}
                title="Fechar"
              >
                x
              </button>
            </div>

            {availableFlows.length > 0 && (
              <div className="toolbar__dialog-search">
                <input
                  ref={loadSearchInputRef}
                  className="toolbar__dialog-search-input"
                  type="text"
                  value={loadFilter}
                  onChange={(event) => setLoadFilter(event.target.value)}
                  placeholder="Buscar por nome ou id"
                  aria-label="Buscar fluxos"
                />
                <span className="toolbar__dialog-search-count">
                  {filteredFlows.length} de {availableFlows.length}
                </span>
              </div>
            )}

            {availableFlows.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhum fluxo salvo foi encontrado.
              </div>
            ) : filteredFlows.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhum fluxo corresponde ao filtro informado.
              </div>
            ) : (
              <div className="toolbar__dialog-list" role="listbox" aria-label="Fluxos salvos">
                {filteredFlows.map((flow) => (
                  <button
                    key={flow.id}
                    className={`toolbar__flow-option ${selectedFlowId === flow.id ? 'toolbar__flow-option--selected' : ''}`}
                    onClick={() => setSelectedFlowId(flow.id)}
                    onDoubleClick={() => { void handleSelectFlow(flow.id); }}
                    type="button"
                  >
                    <span className="toolbar__flow-option-header">
                      <span className="toolbar__flow-option-title">{flow.name?.trim() || 'Untitled Flow'}</span>
                      {isDemoFlow(flow) && <span className="toolbar__flow-option-badge">Demo</span>}
                    </span>
                    <span className="toolbar__flow-option-meta">
                      {flow.nodeCount ?? 0} no(s) • {formatModifiedAt(flow.modifiedAt)}
                    </span>
                    <span className="toolbar__flow-option-id">{flow.id}</span>
                  </button>
                ))}
              </div>
            )}

            <div className="toolbar__dialog-actions">
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => setIsLoadDialogOpen(false)}
                disabled={isApplyingLoadedFlow}
                type="button"
              >
                Cancelar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--primary"
                onClick={() => { void handleSelectFlow(); }}
                disabled={!selectedFlowId || isApplyingLoadedFlow || filteredFlows.length === 0}
                type="button"
              >
                {isApplyingLoadedFlow ? 'Carregando...' : 'Carregar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
