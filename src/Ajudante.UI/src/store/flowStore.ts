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
import { toBackendFlow, fromBackendFlow } from '../bridge/flowConverter';

// ── Helpers ───────────────────────────────────────────────────────

let nodeCounter = 0;
function nextNodeId(): string {
  return `node_${Date.now()}_${++nodeCounter}`;
}

function edgeId(conn: Connection): string {
  return `e_${conn.source}_${conn.sourceHandle ?? 'out'}_${conn.target}_${conn.targetHandle ?? 'in'}`;
}

// ── Store Definition ──────────────────────────────────────────────

export interface FlowState {
  // Flow metadata
  flowId: string;
  flowName: string;
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
  updateNodeProperty: (nodeId: string, propertyId: string, value: any) => void;

  // Persistence
  saveFlow: () => Promise<void>;
  loadFlow: (id: string) => Promise<void>;
  newFlow: () => void;

  // Registry
  nodeDefinitions: NodeDefinition[];
  setNodeDefinitions: (defs: NodeDefinition[]) => void;
}

export const useFlowStore = create<FlowState>((set, get) => ({
  // ── Metadata ────────────────────────────────────────────────────
  flowId: '',
  flowName: 'Untitled Flow',
  setFlowName: (name) => set({ flowName: name }),

  // ── Elements ────────────────────────────────────────────────────
  nodes: [],
  edges: [],

  onNodesChange: (changes) => {
    set({ nodes: applyNodeChanges(changes, get().nodes) as Node<FlowNodeData>[] });

    // Track selection changes
    for (const change of changes) {
      if (change.type === 'select' && change.selected) {
        set({ selectedNodeId: change.id });
      }
    }
  },

  onEdgesChange: (changes) => {
    set({ edges: applyEdgeChanges(changes, get().edges) });
  },

  onConnect: (connection) => {
    const edge: Edge = {
      ...connection,
      id: edgeId(connection),
      type: 'smoothstep',
      animated: false,
    };
    set({ edges: addEdge(edge, get().edges) });
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
    const propertyValues: Record<string, any> = {};
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

    set({ nodes: [...get().nodes, newNode] });
  },

  removeNode: (id) => {
    set({
      nodes: get().nodes.filter((n) => n.id !== id),
      edges: get().edges.filter((e) => e.source !== id && e.target !== id),
      selectedNodeId: get().selectedNodeId === id ? null : get().selectedNodeId,
    });
  },

  updateNodeProperty: (nodeId, propertyId, value) => {
    set({
      nodes: get().nodes.map((n) => {
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
      }),
    });
  },

  // ── Persistence ─────────────────────────────────────────────────
  saveFlow: async () => {
    const { flowId, flowName, nodes, edges } = get();
    const backendFlow = toBackendFlow(flowId, flowName, nodes, edges);
    const result = await sendCommand('flow', 'saveFlow', backendFlow);
    // If the backend returned an id (e.g. for a newly created flow), store it
    if (result?.id && !flowId) {
      set({ flowId: result.id });
    }
  },

  loadFlow: async (id) => {
    const data = await sendCommand('flow', 'loadFlow', { id });
    if (!data || !data.nodes) return;

    const { nodes, edges } = fromBackendFlow(data, get().nodeDefinitions);
    set({
      flowId: data.id ?? id,
      flowName: data.name ?? 'Untitled Flow',
      nodes,
      edges,
      selectedNodeId: null,
    });
  },

  newFlow: () => {
    set({
      flowId: crypto.randomUUID(),
      flowName: 'Untitled Flow',
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });
  },

  // ── Registry ────────────────────────────────────────────────────
  nodeDefinitions: [],
  setNodeDefinitions: (defs) => set({ nodeDefinitions: defs }),
}));
