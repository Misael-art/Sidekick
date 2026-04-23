import { create } from 'zustand';
import {
  type Node,
  type Edge,
  type OnNodesChange,
  type OnEdgesChange,
  type OnConnect,
  applyNodeChanges,
  applyEdgeChanges,
  addEdge,
  type Connection,
} from '@xyflow/react';
import type { FlowNodeData, NodeDefinition } from '../bridge/types';
import { sendCommand } from '../bridge/bridge';
import { toBackendFlow, fromBackendFlow, type BackendFlow } from '../bridge/flowConverter';

// ── Helpers ───────────────────────────────────────────────────────

let nodeCounter = 0;
function nextNodeId(): string {
  return `node_${Date.now()}_${++nodeCounter}`;
}

function edgeId(conn: Connection): string {
  return `e_${conn.source}_${conn.sourceHandle ?? 'out'}_${conn.target}_${conn.targetHandle ?? 'in'}`;
}

function createEmptyFlowState(flowId: string, flowName: string) {
  return {
    flowId,
    flowName,
    nodes: [],
    edges: [],
    selectedNodeId: null,
  };
}

function normalizeRecord(value: Record<string, unknown>): Record<string, unknown> {
  return Object.keys(value)
    .sort((a, b) => a.localeCompare(b))
    .reduce<Record<string, unknown>>((acc, key) => {
      const current = value[key];
      acc[key] = isPlainObject(current) ? normalizeRecord(current) : current;
      return acc;
    }, {});
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function createFlowSnapshot(
  flowId: string,
  flowName: string,
  nodes: Node<FlowNodeData>[],
  edges: Edge[],
): string {
  return JSON.stringify({
    flowId,
    flowName,
    nodes: nodes
      .map((node) => ({
        id: node.id,
        typeId: node.data.typeId,
        position: { x: node.position.x, y: node.position.y },
        propertyValues: normalizeRecord(node.data.propertyValues ?? {}),
      }))
      .sort((a, b) => a.id.localeCompare(b.id)),
    edges: edges
      .map((edge) => ({
        id: edge.id,
        source: edge.source,
        sourceHandle: edge.sourceHandle ?? 'out',
        target: edge.target,
        targetHandle: edge.targetHandle ?? 'in',
      }))
      .sort((a, b) => a.id.localeCompare(b.id)),
  });
}

interface FlowResponse {
  id?: string;
  name?: string;
  flowId?: string;
  nodes?: unknown[];
}

function isBackendFlow(value: FlowResponse): value is BackendFlow {
  return Array.isArray(value.nodes);
}

// ── Store Definition ──────────────────────────────────────────────

export interface FlowState {
  // Flow metadata
  flowId: string;
  flowName: string;
  isDirty: boolean;
  lastPersistedSnapshot: string;
  setFlowName: (name: string) => void;

  // React Flow elements
  nodes: Node<FlowNodeData>[];
  edges: Edge[];
  onNodesChange: OnNodesChange<Node<FlowNodeData>>;
  onEdgesChange: OnEdgesChange;
  onConnect: OnConnect;

  // Selection
  selectedNodeId: string | null;
  setSelectedNodeId: (id: string | null) => void;

  // Node CRUD
  addNode: (typeId: string, position: { x: number; y: number }) => void;
  removeNode: (id: string) => void;
  updateNodeProperty: (nodeId: string, propertyId: string, value: unknown) => void;

  // Persistence
  saveFlow: () => Promise<void>;
  loadFlow: (id: string) => Promise<void>;
  newFlow: () => Promise<void>;

  // Registry
  nodeDefinitions: NodeDefinition[];
  setNodeDefinitions: (defs: NodeDefinition[]) => void;
}

export const useFlowStore = create<FlowState>((set, get) => ({
  // ── Metadata ────────────────────────────────────────────────────
  flowId: '',
  flowName: 'Untitled Flow',
  isDirty: false,
  lastPersistedSnapshot: createFlowSnapshot('', 'Untitled Flow', [], []),
  setFlowName: (name) =>
    set((state) => {
      const nextSnapshot = createFlowSnapshot(state.flowId, name, state.nodes, state.edges);
      return {
        flowName: name,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    }),

  // ── Elements ────────────────────────────────────────────────────
  nodes: [],
  edges: [],

  onNodesChange: (changes) => {
    set((state) => {
      const nextNodes = applyNodeChanges(changes, state.nodes) as Node<FlowNodeData>[];
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    });

    // Track selection changes
    for (const change of changes) {
      if (change.type === 'select' && change.selected) {
        set({ selectedNodeId: change.id });
      }
    }
  },

  onEdgesChange: (changes) => {
    set((state) => {
      const nextEdges = applyEdgeChanges(changes, state.edges);
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, state.nodes, nextEdges);
      return {
        edges: nextEdges,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    });
  },

  onConnect: (connection) => {
    const edge: Edge = {
      ...connection,
      id: edgeId(connection),
      type: 'smoothstep',
      animated: false,
    };
    set((state) => {
      const nextEdges = addEdge(edge, state.edges);
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, state.nodes, nextEdges);
      return {
        edges: nextEdges,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    });
  },

  // ── Selection ───────────────────────────────────────────────────
  selectedNodeId: null,
  setSelectedNodeId: (id) => set({ selectedNodeId: id }),

  // ── Node CRUD ───────────────────────────────────────────────────
  addNode: (typeId, position) => {
    const def = get().nodeDefinitions.find((d) => d.typeId === typeId);
    if (!def) {
      console.warn(`[flowStore] Unknown node typeId: ${typeId}`);
      return;
    }

    const categoryToType: Record<string, string> = {
      Trigger: 'triggerNode',
      Logic: 'logicNode',
      Action: 'actionNode',
    };

    // Build initial property values from defaults
    const propertyValues: Record<string, unknown> = {};
    for (const prop of def.properties) {
      propertyValues[prop.id] = prop.defaultValue ?? '';
    }

    const newNode: Node<FlowNodeData> = {
      id: nextNodeId(),
      type: categoryToType[def.category] ?? 'actionNode',
      position,
      data: {
        typeId: def.typeId,
        displayName: def.displayName,
        category: def.category,
        color: def.color,
        inputPorts: def.inputPorts,
        outputPorts: def.outputPorts,
        properties: def.properties,
        propertyValues,
      },
    };

    set((state) => {
      const nextNodes = [...state.nodes, newNode];
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    });
  },

  removeNode: (id) => {
    set((state) => {
      const nextNodes = state.nodes.filter((n) => n.id !== id);
      const nextEdges = state.edges.filter((e) => e.source !== id && e.target !== id);
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, nextEdges);
      return {
        nodes: nextNodes,
        edges: nextEdges,
        selectedNodeId: state.selectedNodeId === id ? null : state.selectedNodeId,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    });
  },

  updateNodeProperty: (nodeId, propertyId, value) => {
    set((state) => {
      const nextNodes = state.nodes.map((n) => {
        if (n.id !== nodeId) return n;
        return {
          ...n,
          data: {
            ...n.data,
            propertyValues: {
              ...n.data.propertyValues,
              [propertyId]: value,
            },
          },
        };
      });
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
      };
    });
  },

  // ── Persistence ─────────────────────────────────────────────────
  saveFlow: async () => {
    const { flowId, flowName, nodes, edges } = get();
    const backendFlow = toBackendFlow(flowId, flowName, nodes, edges);
    const result = await sendCommand('flow', 'saveFlow', backendFlow) as FlowResponse;
    const resolvedFlowId = result?.flowId ?? result?.id ?? backendFlow.id;
    set({
      flowId: resolvedFlowId,
      lastPersistedSnapshot: createFlowSnapshot(resolvedFlowId, flowName, nodes, edges),
      isDirty: false,
    });
  },

  loadFlow: async (id) => {
    const data = await sendCommand('flow', 'loadFlow', { flowId: id }) as FlowResponse;
    if (!data || !isBackendFlow(data)) return;

    const { nodes, edges } = fromBackendFlow(data, get().nodeDefinitions);
    set({
      flowId: data.id ?? id,
      flowName: data.name ?? 'Untitled Flow',
      nodes,
      edges,
      selectedNodeId: null,
      lastPersistedSnapshot: createFlowSnapshot(data.id ?? id, data.name ?? 'Untitled Flow', nodes, edges),
      isDirty: false,
    });
  },

  newFlow: async () => {
    const fallbackId = crypto.randomUUID();
    const fallbackName = 'Untitled Flow';

    const response = await sendCommand('flow', 'newFlow', { name: fallbackName }) as FlowResponse;

    const resolvedFlowId = response?.id ?? fallbackId;
    const resolvedFlowName = response?.name ?? fallbackName;
    set({
      ...createEmptyFlowState(resolvedFlowId, resolvedFlowName),
      lastPersistedSnapshot: createFlowSnapshot(resolvedFlowId, resolvedFlowName, [], []),
      isDirty: false,
    });
  },

  // ── Registry ────────────────────────────────────────────────────
  nodeDefinitions: [],
  setNodeDefinitions: (defs) => set({ nodeDefinitions: defs }),
}));
