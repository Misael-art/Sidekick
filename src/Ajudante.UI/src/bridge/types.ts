// ── Bridge Protocol Types ──────────────────────────────────────────

export interface BridgeMessage {
  type: 'command' | 'event' | 'response';
  channel: 'flow' | 'engine' | 'platform' | 'inspector' | 'registry';
  action: string;
  requestId?: string;
  payload?: unknown;
  error?: string;
}

// ── Node Registry Types ───────────────────────────────────────────

export type PortDataType =
  | 'Flow'
  | 'String'
  | 'Number'
  | 'Boolean'
  | 'Point'
  | 'Image'
  | 'Any';

export interface PortDefinition {
  id: string;
  name: string;
  dataType: PortDataType;
}

export type PropertyType =
  | 'String'
  | 'Integer'
  | 'Float'
  | 'Boolean'
  | 'FilePath'
  | 'FolderPath'
  | 'Hotkey'
  | 'Point'
  | 'Color'
  | 'Dropdown'
  | 'ImageTemplate';

export interface PropertyDefinition {
  id: string;
  name: string;
  type: PropertyType;
  defaultValue?: unknown;
  description?: string;
  options?: string[];
}

export type NodeCategory = 'Trigger' | 'Logic' | 'Action';

export interface NodeDefinition {
  typeId: string;
  displayName: string;
  category: NodeCategory;
  description: string;
  color: string;
  inputPorts: PortDefinition[];
  outputPorts: PortDefinition[];
  properties: PropertyDefinition[];
}

// ── Flow Data Types ───────────────────────────────────────────────

export interface FlowVariable {
  id: string;
  name: string;
  type: PortDataType;
  defaultValue?: unknown;
}

export interface FlowNodeData extends Record<string, unknown> {
  typeId: string;
  displayName: string;
  category: NodeCategory;
  color: string;
  inputPorts: PortDefinition[];
  outputPorts: PortDefinition[];
  properties: PropertyDefinition[];
  propertyValues: Record<string, unknown>;
}

export interface FlowNode {
  id: string;
  type: string;
  position: { x: number; y: number };
  data: FlowNodeData;
}

export interface FlowConnection {
  id: string;
  source: string;
  sourceHandle: string;
  target: string;
  targetHandle: string;
}

export interface FlowData {
  id: string;
  name: string;
  nodes: FlowNode[];
  connections: FlowConnection[];
  variables: FlowVariable[];
  createdAt: string;
  modifiedAt: string;
}

// ── Runtime Types ─────────────────────────────────────────────────

export type NodeStatus = 'Idle' | 'Running' | 'Completed' | 'Error' | 'Skipped';
export type WireNodeStatus = Lowercase<NodeStatus>;

const nodeStatusMap: Record<string, NodeStatus> = {
  idle: 'Idle',
  running: 'Running',
  completed: 'Completed',
  error: 'Error',
  skipped: 'Skipped',
};

export function normalizeNodeStatus(status: string | null | undefined): NodeStatus {
  if (!status) {
    return 'Idle';
  }

  return nodeStatusMap[status.toLowerCase()] ?? 'Idle';
}

export interface LogEntry {
  timestamp: string;
  level: 'info' | 'warning' | 'error' | 'debug';
  message: string;
  nodeId?: string;
}

export type InspectorMode = 'none' | 'mira' | 'snip';
