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
import { normalizeNodeStatus, type NodeDefinition } from './bridge/types';
import { getDevNodeDefinitions } from './devNodeDefinitions';
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

interface EngineStatusResponse {
  isRunning?: boolean;
  currentFlowId?: string | null;
  currentFlowName?: string | null;
}

export default function App() {
  const setNodeDefinitions = useFlowStore((s) => s.setNodeDefinitions);
  const loadFlow = useFlowStore((s) => s.loadFlow);
  const isDirty = useFlowStore((s) => s.isDirty);
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const setNodeStatus = useAppStore((s) => s.setNodeStatus);
  const setRunning = useAppStore((s) => s.setRunning);
  const setRunningFlow = useAppStore((s) => s.setRunningFlow);
  const clearRunningFlow = useAppStore((s) => s.clearRunningFlow);
  const addLog = useAppStore((s) => s.addLog);

  useEffect(() => {
    const webViewWindow = window as WebViewWindow;
    setUnsavedChangesFlag(webViewWindow, isDirty);
    return () => {
      setUnsavedChangesFlag(webViewWindow, false);
    };
  }, [isDirty]);

  useEffect(() => {
    return attachBeforeUnloadGuard(window as WebViewWindow);
  }, []);

  useEffect(() => {
    let isDisposed = false;

    // Initialise WebView2 bridge
    initBridge();

    // Also listen for pushed node definitions (backend pushes on startup)
    const offRegistry = onEvent<NodeDefinition[]>('registry', 'nodeDefinitions', (payload) => {
      setNodeDefinitions(payload);
    });

    // Listen for execution events
    const offNodeStatus = onEvent<{ nodeId: string; status: string }>('engine', 'nodeStatusChanged', (payload) => {
      setNodeStatus(payload.nodeId, normalizeNodeStatus(payload.status));
    });

    const offFlowCompleted = onEvent<{ flowId: string }>('engine', 'flowCompleted', (payload) => {
      setRunning(false);
      clearRunningFlow();
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Flow ${payload.flowId} completed successfully.`,
      });
    });

    const offFlowError = onEvent<{ flowId: string; error: string }>('engine', 'flowError', (payload) => {
      setRunning(false);
      clearRunningFlow();
      addLog({
        timestamp: new Date().toISOString(),
        level: 'error',
        message: `Flow ${payload.flowId} failed: ${payload.error}`,
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

    const bootstrap = async () => {
      let resolvedDefinitions: NodeDefinition[] = [];

      try {
        const defs = await sendCommand<NodeDefinition[]>('registry', 'getNodeDefinitions', {});
        if (Array.isArray(defs) && defs.length > 0) {
          resolvedDefinitions = defs;
          if (!isDisposed) {
            setNodeDefinitions(defs);
          }
        }
      } catch (err) {
        console.warn('[App] Failed to fetch node definitions:', err);
      }

      const webViewWindow = window as WebViewWindow;
      if (!webViewWindow.chrome?.webview && resolvedDefinitions.length === 0) {
        resolvedDefinitions = getDevNodeDefinitions();
        if (!isDisposed) {
          setNodeDefinitions(resolvedDefinitions);
        }
      }

      try {
        const status = await sendCommand<EngineStatusResponse>('engine', 'getStatus', {});
        if (isDisposed) {
          return;
        }

        const running = !!status?.isRunning;
        setRunning(running);

        if (!running) {
          clearRunningFlow();
          return;
        }

        setRunningFlow(status.currentFlowId ?? null, status.currentFlowName ?? null);
        addLog({
          timestamp: new Date().toISOString(),
          level: 'info',
          message: status.currentFlowName
            ? `Fluxo "${status.currentFlowName}" restaurado como em execucao.`
            : 'Um fluxo em execucao foi restaurado.',
        });

        if (status.currentFlowId && resolvedDefinitions.length > 0) {
          await loadFlow(status.currentFlowId);
        }
      } catch (err) {
        console.warn('[App] Failed to fetch engine status:', err);
      }
    };

    void bootstrap();

    return () => {
      isDisposed = true;
      offRegistry();
      offNodeStatus();
      offFlowCompleted();
      offFlowError();
      offLog();
    };
  }, [setNodeDefinitions, loadFlow, setNodeStatus, setRunning, setRunningFlow, clearRunningFlow, addLog]);

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
