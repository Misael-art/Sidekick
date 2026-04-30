/**
 * Bidirectional converter between React Flow format (frontend)
 * and C# Flow model (backend).
 *
 * Frontend format: Node<FlowNodeData> with data.propertyValues, edges with sourceHandle/targetHandle
 * Backend format:  NodeInstance with Properties dict, Connection with SourcePort/TargetPort
 */

import type { Node, Edge } from '@xyflow/react';
import type { FlowNodeData, NodeDefinition } from './types';

const UI_ALIAS_PROPERTY_KEY = '__ui.alias';
const UI_COMMENT_PROPERTY_KEY = '__ui.comment';
const UI_DISABLED_PROPERTY_KEY = '__ui.disabled';

// ── Backend types (mirrors C# models) ────────────────────────────

export interface BackendFlow {
  id: string;
  name: string;
  version: number;
  variables: BackendVariable[];
  nodes: BackendNodeInstance[];
  connections: BackendConnection[];
  createdAt?: string;
  modifiedAt?: string;
}

export interface BackendNodeInstance {
  id: string;
  typeId: string;
  position: { x: number; y: number };
  properties: Record<string, unknown>;
}

export interface BackendConnection {
  id: string;
  sourceNodeId: string;
  sourcePort: string;
  targetNodeId: string;
  targetPort: string;
}

export interface BackendVariable {
  name: string;
  type: string;
  default?: unknown;
}

// ── Frontend → Backend ───────────────────────────────────────────

export function toBackendFlow(
  flowId: string,
  flowName: string,
  nodes: Node<FlowNodeData>[],
  edges: Edge[],
  options?: { persistUiMetadata?: boolean; runtimeView?: boolean },
): BackendFlow {
  const persistUiMetadata = options?.persistUiMetadata ?? false;
  const runtimeView = options?.runtimeView ?? false;
  const convertedNodes = runtimeView ? createRuntimeNodes(nodes) : nodes;
  const convertedEdges = runtimeView ? createRuntimeEdges(nodes, edges) : edges;

  return {
    id: flowId || crypto.randomUUID(),
    name: flowName,
    version: 1,
    variables: [],
    nodes: convertedNodes.map((n) => ({
      id: n.id,
      typeId: n.data.typeId,
      position: { x: n.position.x, y: n.position.y },
      properties: serializeNodeProperties(n.data, persistUiMetadata),
    })),
    connections: convertedEdges.map((e) => ({
      id: e.id,
      sourceNodeId: e.source,
      sourcePort: e.sourceHandle ?? 'out',
      targetNodeId: e.target,
      targetPort: e.targetHandle ?? 'in',
    })),
  };
}

// ── Backend → Frontend ───────────────────────────────────────────

export function fromBackendFlow(
  backend: BackendFlow,
  definitions: NodeDefinition[],
): { nodes: Node<FlowNodeData>[]; edges: Edge[] } {
  const defMap = new Map(definitions.map((d) => [d.typeId, d]));

  const categoryToType: Record<string, string> = {
    Trigger: 'triggerNode',
    Logic: 'logicNode',
    Action: 'actionNode',
  };

  const nodes: Node<FlowNodeData>[] = [];
  for (const inst of backend.nodes) {
    const def = defMap.get(inst.typeId);
    if (!def) {
      console.warn(`[flowConverter] Unknown typeId "${inst.typeId}", skipping node ${inst.id}`);
      continue;
    }
    const uiMetadata = extractNodeUiMetadata(inst.properties);
    nodes.push({
      id: inst.id,
      type: categoryToType[def.category] ?? 'actionNode',
      position: { x: inst.position.x, y: inst.position.y },
      data: {
        typeId: def.typeId,
        displayName: def.displayName,
        nodeAlias: uiMetadata.nodeAlias,
        nodeComment: uiMetadata.nodeComment,
        nodeDisabled: uiMetadata.nodeDisabled,
        category: def.category,
        color: def.color,
        inputPorts: def.inputPorts,
        outputPorts: def.outputPorts,
        properties: def.properties,
        propertyValues: { ...buildDefaults(def), ...uiMetadata.nodeProperties },
      },
    });
  }

  const edges: Edge[] = backend.connections.map((c) => ({
    id: c.id,
    source: c.sourceNodeId,
    sourceHandle: c.sourcePort,
    target: c.targetNodeId,
    targetHandle: c.targetPort,
    type: 'smoothstep',
  }));

  return { nodes, edges };
}

// ── Helpers ──────────────────────────────────────────────────────

function buildDefaults(def: NodeDefinition): Record<string, unknown> {
  const defaults: Record<string, unknown> = {};
  for (const prop of def.properties) {
    defaults[prop.id] = prop.defaultValue ?? '';
  }
  return defaults;
}

function serializeNodeProperties(
  data: FlowNodeData,
  persistUiMetadata: boolean,
): Record<string, unknown> {
  const properties: Record<string, unknown> = { ...(data.propertyValues ?? {}) };
  delete properties[UI_ALIAS_PROPERTY_KEY];
  delete properties[UI_COMMENT_PROPERTY_KEY];
  delete properties[UI_DISABLED_PROPERTY_KEY];

  if (!persistUiMetadata) {
    return properties;
  }

  const alias = data.nodeAlias?.trim() ?? '';
  const comment = data.nodeComment?.trim() ?? '';
  if (alias) {
    properties[UI_ALIAS_PROPERTY_KEY] = alias;
  }
  if (comment) {
    properties[UI_COMMENT_PROPERTY_KEY] = comment;
  }
  if (data.nodeDisabled) {
    properties[UI_DISABLED_PROPERTY_KEY] = true;
  }

  return properties;
}

function extractNodeUiMetadata(properties: Record<string, unknown>): {
  nodeAlias: string;
  nodeComment: string;
  nodeDisabled: boolean;
  nodeProperties: Record<string, unknown>;
} {
  const nodeProperties = { ...properties };
  const nodeAliasRaw = nodeProperties[UI_ALIAS_PROPERTY_KEY];
  const nodeCommentRaw = nodeProperties[UI_COMMENT_PROPERTY_KEY];
  const nodeDisabledRaw = nodeProperties[UI_DISABLED_PROPERTY_KEY];
  delete nodeProperties[UI_ALIAS_PROPERTY_KEY];
  delete nodeProperties[UI_COMMENT_PROPERTY_KEY];
  delete nodeProperties[UI_DISABLED_PROPERTY_KEY];

  return {
    nodeAlias: typeof nodeAliasRaw === 'string' ? nodeAliasRaw : '',
    nodeComment: typeof nodeCommentRaw === 'string' ? nodeCommentRaw : '',
    nodeDisabled: nodeDisabledRaw === true,
    nodeProperties,
  };
}

function createRuntimeNodes(nodes: Node<FlowNodeData>[]): Node<FlowNodeData>[] {
  return nodes.filter((node) => !node.data.nodeDisabled);
}

function createRuntimeEdges(nodes: Node<FlowNodeData>[], edges: Edge[]): Edge[] {
  const nodeMap = new Map(nodes.map((node) => [node.id, node]));
  const disabledNodeIds = new Set(
    nodes.filter((node) => node.data.nodeDisabled).map((node) => node.id),
  );
  const runtimeEdges = edges.filter(
    (edge) => !disabledNodeIds.has(edge.source) && !disabledNodeIds.has(edge.target),
  );

  for (const disabledNodeId of disabledNodeIds) {
    const disabledNode = nodeMap.get(disabledNodeId);
    if (!disabledNode) {
      continue;
    }

    const incoming = edges.filter((edge) => edge.target === disabledNodeId);
    const outgoing = edges.filter((edge) => edge.source === disabledNodeId);
    if (incoming.length !== 1 || outgoing.length !== 1) {
      continue;
    }

    runtimeEdges.push({
      id: `bypass_${disabledNodeId}_${incoming[0].id}_${outgoing[0].id}`,
      source: incoming[0].source,
      sourceHandle: incoming[0].sourceHandle,
      target: outgoing[0].target,
      targetHandle: outgoing[0].targetHandle,
      type: outgoing[0].type ?? incoming[0].type ?? 'smoothstep',
    });
  }

  return runtimeEdges;
}
