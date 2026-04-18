/**
 * Bidirectional converter between React Flow format (frontend)
 * and C# Flow model (backend).
 *
 * Frontend format: Node<FlowNodeData> with data.propertyValues, edges with sourceHandle/targetHandle
 * Backend format:  NodeInstance with Properties dict, Connection with SourcePort/TargetPort
 */

import type { Node, Edge } from '@xyflow/react';
import type { FlowNodeData, NodeDefinition } from './types';

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
  properties: Record<string, any>;
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
  default?: any;
}

// ── Frontend → Backend ───────────────────────────────────────────

export function toBackendFlow(
  flowId: string,
  flowName: string,
  nodes: Node<FlowNodeData>[],
  edges: Edge[],
): BackendFlow {
  return {
    id: flowId || crypto.randomUUID(),
    name: flowName,
    version: 1,
    variables: [],
    nodes: nodes.map((n) => ({
      id: n.id,
      typeId: n.data.typeId,
      position: { x: n.position.x, y: n.position.y },
      properties: { ...(n.data.propertyValues ?? {}) },
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
    nodes.push({
      id: inst.id,
      type: categoryToType[def.category] ?? 'actionNode',
      position: { x: inst.position.x, y: inst.position.y },
      data: {
        typeId: def.typeId,
        displayName: def.displayName,
        category: def.category,
        color: def.color,
        inputPorts: def.inputPorts,
        outputPorts: def.outputPorts,
        properties: def.properties,
        propertyValues: { ...buildDefaults(def), ...inst.properties },
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

function buildDefaults(def: NodeDefinition): Record<string, any> {
  const defaults: Record<string, any> = {};
  for (const prop of def.properties) {
    defaults[prop.id] = prop.defaultValue ?? '';
  }
  return defaults;
}
