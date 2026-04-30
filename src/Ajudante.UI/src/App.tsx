import { useEffect } from 'react';
import { ReactFlowProvider } from '@xyflow/react';
import Toolbar from './components/Toolbar/Toolbar';
import NodePalette from './components/Sidebar/NodePalette';
import FlowCanvas from './components/Canvas/FlowCanvas';
import PropertyPanel from './components/Sidebar/PropertyPanel';
import ExecutionStatus from './components/StatusBar/ExecutionStatus';
import { useFlowStore } from './store/flowStore';
import { useAppStore } from './store/appStore';
import { initBridge, onEvent, sendCommand } from './bridge/bridge';
import {
  type CapturedElement,
  type CapturedRegion,
  type FlowExecutionHistoryEntry,
  normalizeNodeStatus,
  type FlowQueuedEvent,
  type FlowRuntimeSnapshot,
  type InspectionAsset,
  type NodeDefinition,
  type RuntimeErrorEvent,
  type RuntimePhaseEvent,
  type RuntimeStatusSnapshot,
  type SnipAsset,
  type TriggerRuntimeEvent,
} from './bridge/types';
import { resolveNodeDefinitionsForUi } from './utils/nodeDefinitionFallback';
import {
  attachBeforeUnloadGuard,
  setUnsavedChangesFlag,
  type UnsavedChangesWindow,
} from './utils/unsavedChangesGuard';

interface WebViewWindow extends UnsavedChangesWindow {
  chrome?: {
    webview?: object;
  };
}

function formatFlowLabel(flow: { flowName?: string | null; flowId?: string | null }): string {
  if (flow.flowName?.trim()) {
    return flow.flowName.trim();
  }

  return flow.flowId?.trim() || 'Flow';
}

export default function App() {
  const setNodeDefinitions = useFlowStore((s) => s.setNodeDefinitions);
  const loadFlow = useFlowStore((s) => s.loadFlow);
  const isDirty = useFlowStore((s) => s.isDirty);
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const setNodeStatus = useAppStore((s) => s.setNodeStatus);
  const setRuntimeStatus = useAppStore((s) => s.setRuntimeStatus);
  const upsertFlowRuntime = useAppStore((s) => s.upsertFlowRuntime);
  const removeFlowRuntime = useAppStore((s) => s.removeFlowRuntime);
  const setExecutionHistory = useAppStore((s) => s.setExecutionHistory);
  const upsertExecutionHistory = useAppStore((s) => s.upsertExecutionHistory);
  const addLog = useAppStore((s) => s.addLog);
  const setInspectorMode = useAppStore((s) => s.setInspectorMode);
  const setCapturedElement = useAppStore((s) => s.setCapturedElement);
  const setCapturedRegion = useAppStore((s) => s.setCapturedRegion);
  const setSnipAssets = useAppStore((s) => s.setSnipAssets);
  const upsertSnipAsset = useAppStore((s) => s.upsertSnipAsset);
  const setInspectionAssets = useAppStore((s) => s.setInspectionAssets);
  const upsertInspectionAsset = useAppStore((s) => s.upsertInspectionAsset);
  const setUserMessage = useAppStore((s) => s.setUserMessage);

  useEffect(() => {
    const webViewWindow = window as WebViewWindow;
    setUnsavedChangesFlag(webViewWindow, isDirty);
    return () => {
      setUnsavedChangesFlag(webViewWindow, false);
    };
  }, [isDirty]);

  useEffect(() => attachBeforeUnloadGuard(window as WebViewWindow), []);

  useEffect(() => {
    let isDisposed = false;

    initBridge();

    const offRegistry = onEvent<NodeDefinition[]>('registry', 'nodeDefinitions', (payload) => {
      const resolved = resolveNodeDefinitionsForUi(payload);
      setNodeDefinitions(resolved.definitions);
      if (resolved.usedFallback) {
        addLog({
          timestamp: new Date().toISOString(),
          level: 'warning',
          message: 'Registro de nodes vazio; usando catalogo local embutido para manter a criacao de flows ativa.',
        });
      }
    });

    const offNodeStatus = onEvent<{ nodeId: string; status: string }>('engine', 'nodeStatusChanged', (payload) => {
      setNodeStatus(payload.nodeId, normalizeNodeStatus(payload.status));
    });

    const offElementCaptured = onEvent<CapturedElement>('inspector', 'elementCaptured', (payload) => {
      setInspectorMode('none');
      setCapturedElement(payload);
      setUserMessage({
        type: 'success',
        text: `Mira capturou "${payload.name || payload.automationId || payload.controlType || 'elemento'}". Clique com o botao direito no canvas para criar um node ja preenchido.`,
      });

      if (payload.assetSaveError) {
        addLog({
          timestamp: new Date().toISOString(),
          level: 'warning',
          message: `Elemento capturado, mas o ativo Mira nao foi persistido: ${payload.assetSaveError}`,
        });
      }
    });

    const offRegionCaptured = onEvent<CapturedRegion>('inspector', 'regionCaptured', (payload) => {
      setInspectorMode('none');
      setCapturedRegion(payload);
      setUserMessage({
        type: 'success',
        text: `Snip capturou ${payload.bounds.width} x ${payload.bounds.height}px. Clique com o botao direito no canvas para usar como fallback visual.`,
      });

      if (payload.assetSaveError) {
        addLog({
          timestamp: new Date().toISOString(),
          level: 'warning',
          message: `Snip capturado, mas o ativo nao foi persistido: ${payload.assetSaveError}`,
        });
      }
    });

    const offSnipAssetSaved = onEvent<SnipAsset>('assets', 'snipAssetSaved', (payload) => {
      upsertSnipAsset(payload);
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Ativo Snip salvo: ${payload.displayName}`,
      });
    });

    const offInspectionAssetSaved = onEvent<InspectionAsset>('assets', 'inspectionAssetSaved', (payload) => {
      upsertInspectionAsset(payload);
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Ativo Mira salvo: ${payload.displayName}`,
      });
    });

    const offFlowCompleted = onEvent<{ flowId: string }>('engine', 'flowCompleted', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Flow ${payload.flowId} concluido com sucesso.`,
      });
    });

    const offFlowError = onEvent<{ flowId: string; error: string }>('engine', 'flowError', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'error',
        message: `Flow ${payload.flowId} falhou: ${payload.error}`,
      });
    });

    const offLog = onEvent<{ nodeId: string; message: string }>('engine', 'logMessage', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: payload.message,
        nodeId: payload.nodeId,
      });
    });

    const offRuntimeStatus = onEvent<RuntimeStatusSnapshot>('engine', 'runtimeStatusChanged', (payload) => {
      setRuntimeStatus(payload);
    });

    const offRuntimePhase = onEvent<RuntimePhaseEvent>('engine', 'runtimePhaseChanged', (payload) => {
      addLog({
        timestamp: payload.timestamp ?? new Date().toISOString(),
        level: 'info',
        message: payload.message ? `${payload.phase}: ${payload.message}` : payload.phase,
        nodeId: payload.nodeId,
      });
    });

    const offFlowArmed = onEvent<FlowRuntimeSnapshot>('engine', 'flowArmed', (payload) => {
      upsertFlowRuntime(payload);
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Monitoramento ativado para "${formatFlowLabel(payload)}".`,
      });
    });

    const offFlowDisarmed = onEvent<FlowRuntimeSnapshot>('engine', 'flowDisarmed', (payload) => {
      upsertFlowRuntime(payload);
      if (!payload.isArmed && !payload.isRunning && !payload.queuePending && !payload.lastError) {
        removeFlowRuntime(payload.flowId);
      }

      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Monitoramento desativado para "${formatFlowLabel(payload)}".`,
      });
    });

    const offTriggerFired = onEvent<TriggerRuntimeEvent>('engine', 'triggerFired', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Trigger "${payload.triggerNodeId}" disparou em "${formatFlowLabel(payload)}".`,
      });
    });

    const offFlowQueued = onEvent<FlowQueuedEvent>('engine', 'flowQueued', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Execucao enfileirada para "${formatFlowLabel(payload)}" (${payload.source}).`,
      });
    });

    const offFlowQueueCoalesced = onEvent<FlowQueuedEvent>('engine', 'flowQueueCoalesced', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'warning',
        message: `Novo disparo de "${formatFlowLabel(payload)}" foi consolidado na fila.`,
      });
    });

    const offRuntimeError = onEvent<RuntimeErrorEvent>('engine', 'runtimeError', (payload) => {
      addLog({
        timestamp: new Date().toISOString(),
        level: 'error',
        message: payload.flowId
          ? `${formatFlowLabel(payload)}: ${payload.error}`
          : payload.error,
      });
    });

    const offExecutionHistoryRecorded = onEvent<FlowExecutionHistoryEntry>('engine', 'executionHistoryRecorded', (payload) => {
      upsertExecutionHistory(payload);
    });

    const bootstrap = async () => {
      let resolvedDefinitions: NodeDefinition[] = [];

      try {
        const defs = await sendCommand<NodeDefinition[]>('registry', 'getNodeDefinitions', {});
        const resolved = resolveNodeDefinitionsForUi(defs);
        resolvedDefinitions = resolved.definitions;
        if (!isDisposed) {
          setNodeDefinitions(resolved.definitions);
          if (resolved.usedFallback) {
            addLog({
              timestamp: new Date().toISOString(),
              level: 'warning',
              message: 'Registro de nodes vazio; usando catalogo local embutido para manter a criacao de flows ativa.',
            });
          }
        }
      } catch (err) {
        console.warn('[App] Failed to fetch node definitions:', err);
        const resolved = resolveNodeDefinitionsForUi([]);
        resolvedDefinitions = resolved.definitions;
        if (!isDisposed) {
          setNodeDefinitions(resolvedDefinitions);
          addLog({
            timestamp: new Date().toISOString(),
            level: 'warning',
            message: 'Nao foi possivel carregar o registro do host; usando catalogo local embutido.',
          });
        }
      }

      try {
        const runtimeStatus = await sendCommand<RuntimeStatusSnapshot>('engine', 'getRuntimeStatus', {});
        if (isDisposed) {
          return;
        }

        setRuntimeStatus(runtimeStatus);

        const runningFlowId = runtimeStatus?.currentRun?.flowId;
        if (!runningFlowId || resolvedDefinitions.length === 0) {
          return;
        }

        try {
          await loadFlow(runningFlowId);
        } catch (error) {
          console.warn('[App] Failed to load currently running flow snapshot:', error);
        }
      } catch (err) {
        console.warn('[App] Failed to fetch runtime status:', err);
      }

      try {
        const history = await sendCommand<FlowExecutionHistoryEntry[]>('engine', 'getExecutionHistory', { limit: 20 });
        if (!isDisposed) {
          setExecutionHistory(Array.isArray(history) ? history : []);
        }
      } catch (err) {
        console.warn('[App] Failed to fetch execution history:', err);
      }

      try {
        const assets = await sendCommand<SnipAsset[]>('assets', 'listSnipAssets', {});
        if (!isDisposed) {
          setSnipAssets(Array.isArray(assets) ? assets : []);
        }
      } catch (err) {
        console.warn('[App] Failed to fetch snip assets:', err);
      }

      try {
        const assets = await sendCommand<InspectionAsset[]>('assets', 'listInspectionAssets', {});
        if (!isDisposed) {
          setInspectionAssets(Array.isArray(assets) ? assets : []);
        }
      } catch (err) {
        console.warn('[App] Failed to fetch inspection assets:', err);
      }
    };

    void bootstrap();

    return () => {
      isDisposed = true;
      offRegistry();
      offNodeStatus();
      offElementCaptured();
      offRegionCaptured();
      offSnipAssetSaved();
      offInspectionAssetSaved();
      offFlowCompleted();
      offFlowError();
      offLog();
      offRuntimeStatus();
      offRuntimePhase();
      offFlowArmed();
      offFlowDisarmed();
      offTriggerFired();
      offFlowQueued();
      offFlowQueueCoalesced();
      offRuntimeError();
      offExecutionHistoryRecorded();
    };
  }, [
    addLog,
    loadFlow,
    removeFlowRuntime,
    setExecutionHistory,
    setCapturedElement,
    setCapturedRegion,
    setInspectorMode,
    setInspectionAssets,
    setNodeDefinitions,
    setNodeStatus,
    setSnipAssets,
    setRuntimeStatus,
    setUserMessage,
    upsertFlowRuntime,
    upsertExecutionHistory,
    upsertInspectionAsset,
    upsertSnipAsset,
  ]);

  return (
    <ReactFlowProvider>
      <div className="app">
        <Toolbar />
        <div className="app__workspace">
          <NodePalette />
          <div className="app__canvas-area">
            <FlowCanvas />
          </div>
          {selectedNodeId && (
            <PropertyPanel />
          )}
        </div>
        <ExecutionStatus />
      </div>
    </ReactFlowProvider>
  );
}
