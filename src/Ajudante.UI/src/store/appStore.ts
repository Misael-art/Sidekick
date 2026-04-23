import { create } from 'zustand';
import type { NodeStatus, LogEntry, InspectorMode } from '../bridge/types';

export interface UserMessage {
  type: 'info' | 'success' | 'error';
  text: string;
}

export interface AppState {
  // Engine status
  isRunning: boolean;
  setRunning: (running: boolean) => void;
  runningFlowId: string | null;
  runningFlowName: string | null;
  setRunningFlow: (flowId: string | null, flowName: string | null) => void;
  clearRunningFlow: () => void;

  // Per-node execution status
  nodeStatuses: Record<string, NodeStatus>;
  setNodeStatus: (nodeId: string, status: NodeStatus) => void;
  clearNodeStatuses: () => void;

  // Logs
  logs: LogEntry[];
  addLog: (entry: LogEntry) => void;
  clearLogs: () => void;

  // Inspector
  inspectorMode: InspectorMode;
  setInspectorMode: (mode: InspectorMode) => void;
  capturedElement: unknown | null;
  setCapturedElement: (el: unknown | null) => void;
  capturedRegion: { image: string; bounds: { x: number; y: number; width: number; height: number } } | null;
  setCapturedRegion: (region: { image: string; bounds: { x: number; y: number; width: number; height: number } } | null) => void;

  // UI state
  isPaletteOpen: boolean;
  togglePalette: () => void;
  isLogsExpanded: boolean;
  toggleLogsExpanded: () => void;
  userMessage: UserMessage | null;
  setUserMessage: (message: UserMessage | null) => void;
}

const MAX_LOGS = 1000;

export const useAppStore = create<AppState>((set, get) => ({
  // ── Engine ──────────────────────────────────────────────────────
  isRunning: false,
  setRunning: (running) => set({ isRunning: running }),
  runningFlowId: null,
  runningFlowName: null,
  setRunningFlow: (flowId, flowName) => set({ runningFlowId: flowId, runningFlowName: flowName }),
  clearRunningFlow: () => set({ runningFlowId: null, runningFlowName: null }),

  // ── Node Statuses ───────────────────────────────────────────────
  nodeStatuses: {},
  setNodeStatus: (nodeId, status) =>
    set({ nodeStatuses: { ...get().nodeStatuses, [nodeId]: status } }),
  clearNodeStatuses: () => set({ nodeStatuses: {} }),

  // ── Logs ────────────────────────────────────────────────────────
  logs: [],
  addLog: (entry) => {
    const current = get().logs;
    const updated = current.length >= MAX_LOGS
      ? [...current.slice(current.length - MAX_LOGS + 1), entry]
      : [...current, entry];
    set({ logs: updated });
  },
  clearLogs: () => set({ logs: [] }),

  // ── Inspector ───────────────────────────────────────────────────
  inspectorMode: 'none',
  setInspectorMode: (mode) => set({ inspectorMode: mode }),
  capturedElement: null,
  setCapturedElement: (el) => set({ capturedElement: el }),
  capturedRegion: null,
  setCapturedRegion: (region) => set({ capturedRegion: region }),

  // ── UI ──────────────────────────────────────────────────────────
  isPaletteOpen: true,
  togglePalette: () => set({ isPaletteOpen: !get().isPaletteOpen }),
  isLogsExpanded: false,
  toggleLogsExpanded: () => set({ isLogsExpanded: !get().isLogsExpanded }),
  userMessage: null,
  setUserMessage: (message) => set({ userMessage: message }),
}));
