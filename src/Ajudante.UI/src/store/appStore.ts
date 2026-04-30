import { create } from 'zustand';
import {
  type CapturedElement,
  type FlowExecutionHistoryEntry,
  normalizeFlowRuntimeSnapshot,
  normalizeFlowExecutionHistoryEntry,
  normalizeRuntimeStatusSnapshot,
  type CapturedRegion,
  type CurrentRunSnapshot,
  type FlowRuntimeSnapshot,
  type InspectionAsset,
  type InspectorMode,
  type LogEntry,
  type NodeStatus,
  type RuntimeStatusSnapshot,
  type SnipAsset,
} from '../bridge/types';

export interface UserMessage {
  type: 'info' | 'success' | 'error';
  text: string;
}

function sortFlowRuntimes(flowRuntimes: Record<string, FlowRuntimeSnapshot>): FlowRuntimeSnapshot[] {
  return Object.values(flowRuntimes).sort((left, right) => {
    if (left.isRunning !== right.isRunning) {
      return left.isRunning ? -1 : 1;
    }

    if (left.queuePending !== right.queuePending) {
      return left.queuePending ? -1 : 1;
    }

    if (left.isArmed !== right.isArmed) {
      return left.isArmed ? -1 : 1;
    }

    return left.flowName.localeCompare(right.flowName, 'pt-BR');
  });
}

function createRuntimeMap(snapshot: RuntimeStatusSnapshot): Record<string, FlowRuntimeSnapshot> {
  return snapshot.flows.reduce<Record<string, FlowRuntimeSnapshot>>((acc, flow) => {
    if (flow.flowId) {
      acc[flow.flowId] = flow;
    }

    return acc;
  }, {});
}

function createRuntimeSnapshotFromMap(
  flowRuntimes: Record<string, FlowRuntimeSnapshot>,
  currentRun: CurrentRunSnapshot | null,
  queueLength: number,
): RuntimeStatusSnapshot {
  const flows = sortFlowRuntimes(flowRuntimes);
  return {
    isRunning: Boolean(currentRun),
    queueLength,
    armedFlowCount: flows.filter((flow) => flow.isArmed).length,
    currentRun,
    flows,
  };
}

function shouldKeepFlowRuntime(flow: FlowRuntimeSnapshot): boolean {
  return flow.isArmed || flow.isRunning || flow.queuePending || Boolean(flow.lastError);
}

const emptyRuntimeStatus = normalizeRuntimeStatusSnapshot(null);
const MAX_LOGS = 1000;
const MAX_EXECUTION_HISTORY = 50;

export interface AppState {
  runtimeStatus: RuntimeStatusSnapshot;
  isRunning: boolean;
  queueLength: number;
  armedFlowCount: number;
  currentRun: CurrentRunSnapshot | null;
  flowRuntimes: Record<string, FlowRuntimeSnapshot>;
  executionHistory: FlowExecutionHistoryEntry[];
  setRuntimeStatus: (snapshot: Partial<RuntimeStatusSnapshot> | RuntimeStatusSnapshot | null | undefined) => void;
  upsertFlowRuntime: (snapshot: Partial<FlowRuntimeSnapshot> | FlowRuntimeSnapshot | null | undefined) => void;
  removeFlowRuntime: (flowId: string) => void;
  setExecutionHistory: (entries: Array<Partial<FlowExecutionHistoryEntry> | FlowExecutionHistoryEntry>) => void;
  upsertExecutionHistory: (entry: Partial<FlowExecutionHistoryEntry> | FlowExecutionHistoryEntry | null | undefined) => void;

  nodeStatuses: Record<string, NodeStatus>;
  setNodeStatus: (nodeId: string, status: NodeStatus) => void;
  clearNodeStatuses: () => void;

  logs: LogEntry[];
  addLog: (entry: LogEntry) => void;
  clearLogs: () => void;

  inspectorMode: InspectorMode;
  setInspectorMode: (mode: InspectorMode) => void;
  capturedElement: CapturedElement | null;
  setCapturedElement: (el: CapturedElement | null) => void;
  capturedRegion: CapturedRegion | null;
  setCapturedRegion: (region: CapturedRegion | null) => void;
  snipAssets: SnipAsset[];
  setSnipAssets: (assets: SnipAsset[]) => void;
  upsertSnipAsset: (asset: SnipAsset) => void;
  inspectionAssets: InspectionAsset[];
  setInspectionAssets: (assets: InspectionAsset[]) => void;
  upsertInspectionAsset: (asset: InspectionAsset) => void;

  isPaletteOpen: boolean;
  togglePalette: () => void;
  isLogsExpanded: boolean;
  toggleLogsExpanded: () => void;
  userMessage: UserMessage | null;
  setUserMessage: (message: UserMessage | null) => void;
}

export const useAppStore = create<AppState>((set, get) => ({
  runtimeStatus: emptyRuntimeStatus,
  isRunning: false,
  queueLength: 0,
  armedFlowCount: 0,
  currentRun: null,
  flowRuntimes: {},
  executionHistory: [],
  setRuntimeStatus: (snapshot) => {
    const normalized = normalizeRuntimeStatusSnapshot(snapshot);
    set({
      runtimeStatus: normalized,
      isRunning: normalized.isRunning,
      queueLength: normalized.queueLength,
      armedFlowCount: normalized.armedFlowCount,
      currentRun: normalized.currentRun ?? null,
      flowRuntimes: createRuntimeMap(normalized),
    });
  },
  upsertFlowRuntime: (snapshot) => {
    const normalized = normalizeFlowRuntimeSnapshot(snapshot);
    if (!normalized.flowId) {
      return;
    }

    set((state) => {
      const nextMap = { ...state.flowRuntimes };

      if (shouldKeepFlowRuntime(normalized)) {
        nextMap[normalized.flowId] = normalized;
      } else {
        delete nextMap[normalized.flowId];
      }

      const nextRuntimeStatus = createRuntimeSnapshotFromMap(nextMap, state.currentRun, state.queueLength);
      return {
        flowRuntimes: nextMap,
        armedFlowCount: nextRuntimeStatus.armedFlowCount,
        runtimeStatus: nextRuntimeStatus,
      };
    });
  },
  removeFlowRuntime: (flowId) => {
    if (!flowId) {
      return;
    }

    set((state) => {
      const nextMap = { ...state.flowRuntimes };
      delete nextMap[flowId];
      const nextRuntimeStatus = createRuntimeSnapshotFromMap(nextMap, state.currentRun, state.queueLength);
      return {
        flowRuntimes: nextMap,
        armedFlowCount: nextRuntimeStatus.armedFlowCount,
        runtimeStatus: nextRuntimeStatus,
      };
    });
  },
  setExecutionHistory: (entries) => {
    const normalized = entries
      .map((entry) => normalizeFlowExecutionHistoryEntry(entry))
      .filter((entry) => entry.runId)
      .sort((left, right) => right.startedAt.localeCompare(left.startedAt))
      .slice(0, MAX_EXECUTION_HISTORY);

    set({ executionHistory: normalized });
  },
  upsertExecutionHistory: (entry) => {
    const normalized = normalizeFlowExecutionHistoryEntry(entry);
    if (!normalized.runId) {
      return;
    }

    set((state) => {
      const remaining = state.executionHistory.filter((candidate) => candidate.runId !== normalized.runId);
      return {
        executionHistory: [normalized, ...remaining]
          .sort((left, right) => right.startedAt.localeCompare(left.startedAt))
          .slice(0, MAX_EXECUTION_HISTORY),
      };
    });
  },

  nodeStatuses: {},
  setNodeStatus: (nodeId, status) =>
    set({ nodeStatuses: { ...get().nodeStatuses, [nodeId]: status } }),
  clearNodeStatuses: () => set({ nodeStatuses: {} }),

  logs: [],
  addLog: (entry) => {
    const current = get().logs;
    const updated = current.length >= MAX_LOGS
      ? [...current.slice(current.length - MAX_LOGS + 1), entry]
      : [...current, entry];
    set({ logs: updated });
  },
  clearLogs: () => set({ logs: [] }),

  inspectorMode: 'none',
  setInspectorMode: (mode) => set({ inspectorMode: mode }),
  capturedElement: null,
  setCapturedElement: (el) => set({ capturedElement: el }),
  capturedRegion: null,
  setCapturedRegion: (region) => set({ capturedRegion: region }),
  snipAssets: [],
  setSnipAssets: (assets) => set({ snipAssets: [...assets].sort((left, right) => right.updatedAt.localeCompare(left.updatedAt)) }),
  upsertSnipAsset: (asset) =>
    set((state) => {
      const remaining = state.snipAssets.filter((candidate) => candidate.id !== asset.id);
      return {
        snipAssets: [asset, ...remaining].sort((left, right) => right.updatedAt.localeCompare(left.updatedAt)),
      };
    }),
  inspectionAssets: [],
  setInspectionAssets: (assets) => set({ inspectionAssets: [...assets].sort((left, right) => right.updatedAt.localeCompare(left.updatedAt)) }),
  upsertInspectionAsset: (asset) =>
    set((state) => {
      const remaining = state.inspectionAssets.filter((candidate) => candidate.id !== asset.id);
      return {
        inspectionAssets: [asset, ...remaining].sort((left, right) => right.updatedAt.localeCompare(left.updatedAt)),
      };
    }),

  isPaletteOpen: true,
  togglePalette: () => set({ isPaletteOpen: !get().isPaletteOpen }),
  isLogsExpanded: false,
  toggleLogsExpanded: () => set({ isLogsExpanded: !get().isLogsExpanded }),
  userMessage: null,
  setUserMessage: (message) => set({ userMessage: message }),
}));
