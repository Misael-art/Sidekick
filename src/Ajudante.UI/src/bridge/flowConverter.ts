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
  options?: { persistUiMetadata?: boolean },
): BackendFlow {
  const persistUiMetadata = options?.persistUiMetadata ?? false;

  return {
    id: flowId || crypto.randomUUID(),
    name: flowName,
    version: 1,
    variables: [],
    nodes: nodes.map((n) => ({
      id: n.id,
      typeId: n.data.typeId,
      position: { x: n.position.x, y: n.position.y },
      properties: serializeNodeProperties(n.data, persistUiMetadata),
    })),
    connections: edges.map((e) => ({
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

  return properties;
}

function extractNodeUiMetadata(properties: Record<string, unknown>): {
  nodeAlias: string;
  nodeComment: string;
  nodeProperties: Record<string, unknown>;
} {
  const nodeProperties = { ...properties };
  const nodeAliasRaw = nodeProperties[UI_ALIAS_PROPERTY_KEY];
  const nodeCommentRaw = nodeProperties[UI_COMMENT_PROPERTY_KEY];
  delete nodeProperties[UI_ALIAS_PROPERTY_KEY];
  delete nodeProperties[UI_COMMENT_PROPERTY_KEY];

  return {
    nodeAlias: typeof nodeAliasRaw === 'string' ? nodeAliasRaw : '',
    nodeComment: typeof nodeCommentRaw === 'string' ? nodeCommentRaw : '',
    nodeProperties,
  };
}
