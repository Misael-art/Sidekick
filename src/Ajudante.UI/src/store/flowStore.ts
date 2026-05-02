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
import type { FlowNodeData, FlowValidationResult, NodeDefinition } from '../bridge/types';
import { sendCommand } from '../bridge/bridge';
import { toBackendFlow, fromBackendFlow, type BackendFlow, type StickyNoteData } from '../bridge/flowConverter';

// ── Helpers ───────────────────────────────────────────────────────

let nodeCounter = 0;
function nextNodeId(): string {
  return `node_${Date.now()}_${++nodeCounter}`;
}

function edgeId(conn: Connection): string {
  return `e_${conn.source}_${conn.sourceHandle ?? 'out'}_${conn.target}_${conn.targetHandle ?? 'in'}`;
}

interface FlowMutationState {
  flowId: string;
  flowName: string;
  lastPersistedSnapshot: string;
}

export interface ConnectionAttemptResult {
  ok: boolean;
  reason?: string;
}

function createEmptyFlowState(flowId: string, flowName: string) {
  return {
    flowId,
    flowName,
    nodes: [],
    edges: [],
    selectedNodeId: null,
    validationResult: null,
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
  stickyNotes: Node<StickyNoteData>[] = [],
): string {
  return JSON.stringify({
    flowId,
    flowName,
    nodes: nodes
      .map((node) => ({
        id: node.id,
        typeId: node.data.typeId,
        position: { x: node.position.x, y: node.position.y },
        nodeAlias: node.data.nodeAlias ?? '',
        nodeComment: node.data.nodeComment ?? '',
        nodeDisabled: node.data.nodeDisabled ?? false,
        propertyValues: normalizeRecord(node.data.propertyValues ?? {}),
      }))
      .sort((a, b) => a.id.localeCompare(b.id)),
    stickies: stickyNotes
      .map((s) => ({
        id: s.id,
        position: { x: s.position.x, y: s.position.y },
        title: s.data.title ?? '',
        body: s.data.body ?? '',
        color: s.data.color ?? 'yellow',
        width: s.data.width ?? 240,
        height: s.data.height ?? 160,
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

function createDirtyPatch(
  state: FlowMutationState & { stickyNotes?: Node<StickyNoteData>[] },
  nodes: Node<FlowNodeData>[],
  edges: Edge[],
  stickyNotes: Node<StickyNoteData>[] = state.stickyNotes ?? [],
): { nodes: Node<FlowNodeData>[]; edges: Edge[]; isDirty: boolean; validationResult: null } {
  const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nodes, edges, stickyNotes);
  return {
    nodes,
    edges,
    isDirty: nextSnapshot !== state.lastPersistedSnapshot,
    validationResult: null,
  };
}

function cloneRecord(value: Record<string, unknown>): Record<string, unknown> {
  if (typeof structuredClone === 'function') {
    return structuredClone(value) as Record<string, unknown>;
  }

  return JSON.parse(JSON.stringify(value)) as Record<string, unknown>;
}

function buildNodeFromDefinition(
  def: NodeDefinition,
  position: { x: number; y: number },
  propertyOverrides: Record<string, unknown> = {},
  nodeId = nextNodeId(),
): Node<FlowNodeData> {
  const categoryToType: Record<string, string> = {
    Trigger: 'triggerNode',
    Logic: 'logicNode',
    Action: 'actionNode',
  };

  const propertyValues: Record<string, unknown> = {};
  for (const prop of def.properties) {
    propertyValues[prop.id] = prop.defaultValue ?? '';
  }

  for (const [key, value] of Object.entries(propertyOverrides)) {
    if (def.properties.some((property) => property.id === key)) {
      propertyValues[key] = value;
    }
  }

  return {
    id: nodeId,
    type: categoryToType[def.category] ?? 'actionNode',
    position,
    data: {
      typeId: def.typeId,
      displayName: def.displayName,
      nodeAlias: '',
      nodeComment: '',
      nodeDisabled: false,
      category: def.category,
      color: def.color,
      inputPorts: def.inputPorts,
      outputPorts: def.outputPorts,
      properties: def.properties,
      propertyValues,
    },
  };
}

function isPortCompatible(sourceType: string, targetType: string): boolean {
  if (sourceType === 'Flow' || targetType === 'Flow') {
    return sourceType === 'Flow' && targetType === 'Flow';
  }

  return sourceType === 'Any' || targetType === 'Any' || sourceType === targetType;
}

export function validateConnectionForNodes(
  nodes: Node<FlowNodeData>[],
  connection: Connection,
): ConnectionAttemptResult {
  const sourceNode = nodes.find((node) => node.id === connection.source);
  const targetNode = nodes.find((node) => node.id === connection.target);

  if (!connection.source || !connection.target || !sourceNode || !targetNode) {
    return { ok: false, reason: 'Conexao invalida: node de origem ou destino nao encontrado.' };
  }

  if (connection.source === connection.target) {
    return { ok: false, reason: 'Conexao invalida: um node nao pode conectar nele mesmo.' };
  }

  if (sourceNode.data.nodeDisabled || targetNode.data.nodeDisabled) {
    return { ok: false, reason: 'Conexao invalida: habilite os nodes antes de conectar.' };
  }

  const sourcePortId = connection.sourceHandle ?? sourceNode.data.outputPorts[0]?.id;
  const targetPortId = connection.targetHandle ?? targetNode.data.inputPorts[0]?.id;
  const sourcePort = sourceNode.data.outputPorts.find((port) => port.id === sourcePortId);
  const targetPort = targetNode.data.inputPorts.find((port) => port.id === targetPortId);

  if (!sourcePort) {
    return { ok: false, reason: `Conexao invalida: porta de saida "${sourcePortId ?? ''}" nao existe.` };
  }

  if (!targetPort) {
    return { ok: false, reason: `Conexao invalida: porta de entrada "${targetPortId ?? ''}" nao existe.` };
  }

  if (!isPortCompatible(sourcePort.dataType, targetPort.dataType)) {
    return {
      ok: false,
      reason: `Portas incompativeis: ${sourcePort.name} (${sourcePort.dataType}) nao conecta em ${targetPort.name} (${targetPort.dataType}).`,
    };
  }

  return { ok: true };
}

function createEdgeFromConnection(connection: Connection, id = edgeId(connection)): Edge {
  return {
    ...connection,
    id,
    type: 'smoothstep',
    animated: false,
  };
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

const requiredRuntimeNodeTypeIds = [
  'action.captureScreenshot',
  'action.recordDesktop',
  'action.recordCamera',
  'logic.conditionGroup',
] as const;

// ── Store Definition ──────────────────────────────────────────────

export interface FlowState {
  // Flow metadata
  flowId: string;
  flowName: string;
  isDirty: boolean;
  lastPersistedSnapshot: string;
  validationResult: FlowValidationResult | null;
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
  addNode: (typeId: string, position: { x: number; y: number }, propertyOverrides?: Record<string, unknown>) => string | null;
  removeNode: (id: string) => void;
  duplicateNode: (id: string) => string | null;
  toggleNodeDisabled: (id: string, disabled?: boolean) => void;
  updateNodeProperty: (nodeId: string, propertyId: string, value: unknown) => void;
  updateNodeProperties: (nodeId: string, propertyValues: Record<string, unknown>) => void;
  updateNodeMetadata: (nodeId: string, metadata: { nodeAlias?: string; nodeComment?: string }) => void;
  insertNodeOnEdge: (edgeId: string, typeId: string, position: { x: number; y: number }, propertyOverrides?: Record<string, unknown>) => string | null;
  removeEdge: (edgeId: string) => void;
  connectNodes: (connection: Connection) => ConnectionAttemptResult;
  canConnect: (connection: Connection) => ConnectionAttemptResult;
  reconnectEdge: (edgeId: string, connection: Connection) => ConnectionAttemptResult;
  autoLayout: () => void;

  // Persistence
  saveFlow: () => Promise<void>;
  loadFlow: (id: string) => Promise<void>;
  newFlow: () => Promise<void>;
  validateFlow: () => Promise<FlowValidationResult>;

  // Registry
  nodeDefinitions: NodeDefinition[];
  setNodeDefinitions: (defs: NodeDefinition[]) => void;

  // Sticky notes
  stickyNotes: Node<StickyNoteData>[];
  addStickyNote: (position: { x: number; y: number }, init?: Partial<StickyNoteData>) => string;
  duplicateStickyNote: (id: string) => string | null;
  updateStickyNote: (id: string, patch: Partial<StickyNoteData>) => void;
  removeStickyNote: (id: string) => void;
  applyStickyNoteChange: (changes: { id: string; position?: { x: number; y: number }; selected?: boolean }[]) => void;
}

export const useFlowStore = create<FlowState>((set, get) => ({
  // ── Metadata ────────────────────────────────────────────────────
  flowId: '',
  flowName: 'Untitled Flow',
  isDirty: false,
  stickyNotes: [],
  lastPersistedSnapshot: createFlowSnapshot('', 'Untitled Flow', [], [], []),
  setFlowName: (name) =>
    set((state) => {
      const nextSnapshot = createFlowSnapshot(state.flowId, name, state.nodes, state.edges, state.stickyNotes);
      return {
        flowName: name,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    }),

  // ── Elements ────────────────────────────────────────────────────
  nodes: [],
  edges: [],

  onNodesChange: (changes) => {
    set((state) => {
      const nextNodes = applyNodeChanges(changes, state.nodes) as Node<FlowNodeData>[];
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges, state.stickyNotes);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
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
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, state.nodes, nextEdges, state.stickyNotes);
      return {
        edges: nextEdges,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    });
  },

  onConnect: (connection) => {
    get().connectNodes(connection);
  },

  // ── Selection ───────────────────────────────────────────────────
  selectedNodeId: null,
  validationResult: null,
  setSelectedNodeId: (id) => set({ selectedNodeId: id }),

  // ── Node CRUD ───────────────────────────────────────────────────
  addNode: (typeId, position, propertyOverrides = {}) => {
    const def = get().nodeDefinitions.find((d) => d.typeId === typeId);
    if (!def) {
      console.warn(`[flowStore] Unknown node typeId: ${typeId}`);
      return null;
    }

    const newNode = buildNodeFromDefinition(def, position, propertyOverrides);

    set((state) => {
      const nextNodes = [...state.nodes, newNode];
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges, state.stickyNotes);
      return {
        nodes: nextNodes,
        selectedNodeId: newNode.id,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    });

    return newNode.id;
  },

  removeNode: (id) => {
    set((state) => {
      const nextNodes = state.nodes.filter((n) => n.id !== id);
      const nextEdges = state.edges.filter((e) => e.source !== id && e.target !== id);
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, nextEdges, state.stickyNotes);
      return {
        nodes: nextNodes,
        edges: nextEdges,
        selectedNodeId: state.selectedNodeId === id ? null : state.selectedNodeId,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    });
  },

  duplicateNode: (id) => {
    const source = get().nodes.find((node) => node.id === id);
    if (!source) {
      return null;
    }

    const newNodeId = nextNodeId();
    const duplicate: Node<FlowNodeData> = {
      ...source,
      id: newNodeId,
      selected: false,
      position: {
        x: source.position.x + 40,
        y: source.position.y + 40,
      },
      data: {
        ...source.data,
        nodeAlias: source.data.nodeAlias ? `${source.data.nodeAlias} copia` : '',
        propertyValues: cloneRecord(source.data.propertyValues ?? {}),
      },
    };

    set((state) => {
      const nextNodes = [...state.nodes, duplicate];
      return {
        ...createDirtyPatch(state, nextNodes, state.edges),
        selectedNodeId: newNodeId,
      };
    });

    return newNodeId;
  },

  toggleNodeDisabled: (id, disabled) => {
    set((state) => {
      const nextNodes = state.nodes.map((node) => {
        if (node.id !== id) return node;
        const nextDisabled = disabled ?? !node.data.nodeDisabled;
        return {
          ...node,
          data: {
            ...node.data,
            nodeDisabled: nextDisabled,
          },
        };
      });

      return createDirtyPatch(state, nextNodes, state.edges);
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
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges, state.stickyNotes);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    });
  },

  updateNodeProperties: (nodeId, propertyValues) => {
    set((state) => {
      const nextNodes = state.nodes.map((n) => {
        if (n.id !== nodeId) return n;
        return {
          ...n,
          data: {
            ...n.data,
            propertyValues: {
              ...n.data.propertyValues,
              ...propertyValues,
            },
          },
        };
      });
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges, state.stickyNotes);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    });
  },

  updateNodeMetadata: (nodeId, metadata) => {
    set((state) => {
      const nextNodes = state.nodes.map((n) => {
        if (n.id !== nodeId) return n;
        return {
          ...n,
          data: {
            ...n.data,
            nodeAlias: metadata.nodeAlias ?? n.data.nodeAlias ?? '',
            nodeComment: metadata.nodeComment ?? n.data.nodeComment ?? '',
          },
        };
      });
      const nextSnapshot = createFlowSnapshot(state.flowId, state.flowName, nextNodes, state.edges, state.stickyNotes);
      return {
        nodes: nextNodes,
        isDirty: nextSnapshot !== state.lastPersistedSnapshot,
        validationResult: null,
      };
    });
  },

  insertNodeOnEdge: (targetEdgeId, typeId, position, propertyOverrides = {}) => {
    const state = get();
    const edge = state.edges.find((candidate) => candidate.id === targetEdgeId);
    const def = state.nodeDefinitions.find((candidate) => candidate.typeId === typeId);
    if (!edge || !def) {
      return null;
    }

    const sourceNode = state.nodes.find((node) => node.id === edge.source);
    const targetNode = state.nodes.find((node) => node.id === edge.target);
    const sourcePort = sourceNode?.data.outputPorts.find((port) => port.id === (edge.sourceHandle ?? 'out'));
    const targetPort = targetNode?.data.inputPorts.find((port) => port.id === (edge.targetHandle ?? 'in'));
    if (!sourceNode || !targetNode || !sourcePort || !targetPort) {
      return null;
    }

    const inputPort = def.inputPorts.find((port) => isPortCompatible(sourcePort.dataType, port.dataType));
    const outputPort = def.outputPorts.find((port) => isPortCompatible(port.dataType, targetPort.dataType));
    if (!inputPort || !outputPort) {
      return null;
    }

    const newNode = buildNodeFromDefinition(def, position, propertyOverrides);
    const firstConnection: Connection = {
      source: edge.source,
      sourceHandle: edge.sourceHandle ?? sourcePort.id,
      target: newNode.id,
      targetHandle: inputPort.id,
    };
    const secondConnection: Connection = {
      source: newNode.id,
      sourceHandle: outputPort.id,
      target: edge.target,
      targetHandle: edge.targetHandle ?? targetPort.id,
    };

    set((currentState) => {
      const nextNodes = [...currentState.nodes, newNode];
      const nextEdges = [
        ...currentState.edges.filter((candidate) => candidate.id !== targetEdgeId),
        createEdgeFromConnection(firstConnection),
        createEdgeFromConnection(secondConnection),
      ];
      return {
        ...createDirtyPatch(currentState, nextNodes, nextEdges),
        selectedNodeId: newNode.id,
      };
    });

    return newNode.id;
  },

  removeEdge: (edgeIdToRemove) => {
    set((state) => {
      const nextEdges = state.edges.filter((edge) => edge.id !== edgeIdToRemove);
      return createDirtyPatch(state, state.nodes, nextEdges);
    });
  },

  canConnect: (connection) => validateConnectionForNodes(get().nodes, connection),

  connectNodes: (connection) => {
    const result = validateConnectionForNodes(get().nodes, connection);
    if (!result.ok) {
      return result;
    }

    const edge = createEdgeFromConnection(connection);
    set((state) => {
      const nextEdges = addEdge(edge, state.edges);
      return createDirtyPatch(state, state.nodes, nextEdges);
    });

    return result;
  },

  reconnectEdge: (edgeIdToReconnect, connection) => {
    const state = get();
    const existingEdge = state.edges.find((edge) => edge.id === edgeIdToReconnect);
    if (!existingEdge) {
      return { ok: false, reason: 'Conexao invalida: fio original nao encontrado.' };
    }

    const result = validateConnectionForNodes(state.nodes, connection);
    if (!result.ok) {
      return result;
    }

    set((currentState) => {
      const nextEdges = currentState.edges.map((edge) => (
        edge.id === edgeIdToReconnect
          ? createEdgeFromConnection(connection, edge.id)
          : edge
      ));
      return createDirtyPatch(currentState, currentState.nodes, nextEdges);
    });

    return result;
  },

  autoLayout: () => {
    set((state) => {
      const incomingCount = new Map(state.nodes.map((node) => [node.id, 0]));
      for (const edge of state.edges) {
        incomingCount.set(edge.target, (incomingCount.get(edge.target) ?? 0) + 1);
      }

      const depth = new Map(state.nodes.map((node) => [node.id, 0]));
      const queue = state.nodes
        .filter((node) => (incomingCount.get(node.id) ?? 0) === 0)
        .map((node) => node.id);
      const visited = new Set<string>();

      while (queue.length > 0) {
        const current = queue.shift()!;
        visited.add(current);
        const currentDepth = depth.get(current) ?? 0;

        for (const edge of state.edges.filter((candidate) => candidate.source === current)) {
          depth.set(edge.target, Math.max(depth.get(edge.target) ?? 0, currentDepth + 1));
          incomingCount.set(edge.target, Math.max(0, (incomingCount.get(edge.target) ?? 1) - 1));
          if ((incomingCount.get(edge.target) ?? 0) === 0 && !visited.has(edge.target)) {
            queue.push(edge.target);
          }
        }
      }

      const grouped = new Map<number, Node<FlowNodeData>[]>();
      for (const node of state.nodes) {
        const nodeDepth = depth.get(node.id) ?? 0;
        grouped.set(nodeDepth, [...(grouped.get(nodeDepth) ?? []), node]);
      }

      const indexByNodeId = new Map<string, number>();
      for (const [nodeDepth, group] of grouped) {
        group
          .sort((left, right) => left.position.y - right.position.y || left.id.localeCompare(right.id))
          .forEach((node, index) => {
            indexByNodeId.set(node.id, index);
            depth.set(node.id, nodeDepth);
          });
      }

      const nextNodes = state.nodes.map((node) => ({
        ...node,
        position: {
          x: 80 + (depth.get(node.id) ?? 0) * 280,
          y: 80 + (indexByNodeId.get(node.id) ?? 0) * 140,
        },
      }));

      return createDirtyPatch(state, nextNodes, state.edges);
    });
  },

  // ── Persistence ─────────────────────────────────────────────────
  saveFlow: async () => {
    const { flowId, flowName, nodes, edges, stickyNotes } = get();
    const backendFlow = toBackendFlow(
      flowId,
      flowName,
      [...nodes, ...stickyNotes],
      edges,
      { persistUiMetadata: true },
    );
    const result = await sendCommand('flow', 'saveFlow', backendFlow) as FlowResponse;
    const resolvedFlowId = result?.flowId ?? result?.id ?? backendFlow.id;
    set({
      flowId: resolvedFlowId,
      lastPersistedSnapshot: createFlowSnapshot(resolvedFlowId, flowName, nodes, edges, stickyNotes),
      isDirty: false,
    });
  },

  loadFlow: async (id) => {
    const data = await sendCommand('flow', 'loadFlow', { flowId: id }) as FlowResponse;
    if (!data || !isBackendFlow(data)) return;

    const merged = fromBackendFlow(data, get().nodeDefinitions);
    const flowNodes = merged.nodes.filter((n) => n.type !== 'stickyNote') as Node<FlowNodeData>[];
    const stickyNotes = merged.nodes.filter((n) => n.type === 'stickyNote') as Node<StickyNoteData>[];
    set({
      flowId: data.id ?? id,
      flowName: data.name ?? 'Untitled Flow',
      nodes: flowNodes,
      edges: merged.edges,
      stickyNotes,
      selectedNodeId: null,
      lastPersistedSnapshot: createFlowSnapshot(
        data.id ?? id,
        data.name ?? 'Untitled Flow',
        flowNodes,
        merged.edges,
        stickyNotes,
      ),
      isDirty: false,
      validationResult: null,
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
      stickyNotes: [],
      lastPersistedSnapshot: createFlowSnapshot(resolvedFlowId, resolvedFlowName, [], [], []),
      isDirty: false,
    });
  },

  validateFlow: async () => {
    const { flowId, flowName, nodes, edges } = get();
    const backendFlow = toBackendFlow(flowId, flowName, nodes, edges, { runtimeView: true });
    const result = await sendCommand<FlowValidationResult>('engine', 'validateFlow', backendFlow);
    set({ validationResult: result });
    return result;
  },

  // ── Registry ────────────────────────────────────────────────────
  nodeDefinitions: [],
  setNodeDefinitions: (defs) => {
    const availableIds = new Set(defs.map((definition) => definition.typeId));
    const missingRequired = requiredRuntimeNodeTypeIds.filter((typeId) => !availableIds.has(typeId));
    if (missingRequired.length > 0) {
      console.warn(`[flowStore] Missing required runtime node definitions: ${missingRequired.join(', ')}`);
    }
    set({ nodeDefinitions: defs });
  },

  // ── Sticky Notes ────────────────────────────────────────────────
  addStickyNote: (position, init) => {
    const id = `sticky_${Date.now()}_${++nodeCounter}`;
    const data: StickyNoteData = {
      kind: 'sticky',
      title: init?.title ?? '',
      body: init?.body ?? '',
      color: init?.color ?? 'yellow',
      width: init?.width ?? 240,
      height: init?.height ?? 160,
    };
    set((state) => {
      const next = [...state.stickyNotes, {
        id,
        type: 'stickyNote',
        position,
        data,
        draggable: true,
        selectable: true,
      } as Node<StickyNoteData>];
      const snap = createFlowSnapshot(state.flowId, state.flowName, state.nodes, state.edges, next);
      return {
        stickyNotes: next,
        isDirty: snap !== state.lastPersistedSnapshot,
      };
    });
    return id;
  },

  duplicateStickyNote: (id) => {
    const source = get().stickyNotes.find((sticky) => sticky.id === id);
    if (!source) {
      return null;
    }

    const newId = `sticky_${Date.now()}_${++nodeCounter}`;
    set((state) => {
      const next: Node<StickyNoteData>[] = [
        ...state.stickyNotes.map((sticky) => ({ ...sticky, selected: false })),
        {
          ...source,
          id: newId,
          selected: true,
          position: {
            x: source.position.x + 32,
            y: source.position.y + 32,
          },
          data: {
            ...source.data,
            title: source.data.title ? `${source.data.title} (copia)` : source.data.title,
          },
        } as Node<StickyNoteData>,
      ];
      const snap = createFlowSnapshot(state.flowId, state.flowName, state.nodes, state.edges, next);
      return {
        stickyNotes: next,
        selectedNodeId: null,
        isDirty: snap !== state.lastPersistedSnapshot,
      };
    });

    return newId;
  },

  updateStickyNote: (id, patch) =>
    set((state) => {
      const next = state.stickyNotes.map((s) =>
        s.id === id ? { ...s, data: { ...s.data, ...patch } } : s,
      );
      const snap = createFlowSnapshot(state.flowId, state.flowName, state.nodes, state.edges, next);
      return {
        stickyNotes: next,
        isDirty: snap !== state.lastPersistedSnapshot,
      };
    }),

  removeStickyNote: (id) =>
    set((state) => {
      const next = state.stickyNotes.filter((s) => s.id !== id);
      const snap = createFlowSnapshot(state.flowId, state.flowName, state.nodes, state.edges, next);
      return {
        stickyNotes: next,
        isDirty: snap !== state.lastPersistedSnapshot,
      };
    }),

  applyStickyNoteChange: (changes) =>
    set((state) => {
      let mutated = false;
      const next = state.stickyNotes.map((s) => {
        const change = changes.find((c) => c.id === s.id);
        if (!change) return s;
        const merged = { ...s };
        if (change.position) {
          merged.position = change.position;
          mutated = true;
        }
        if (change.selected !== undefined) {
          merged.selected = change.selected;
        }
        return merged;
      });
      if (!mutated && !changes.some((c) => c.selected !== undefined)) {
        return state;
      }
      const snap = createFlowSnapshot(state.flowId, state.flowName, state.nodes, state.edges, next);
      return {
        stickyNotes: next,
        isDirty: snap !== state.lastPersistedSnapshot,
      };
    }),
}));
