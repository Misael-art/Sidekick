// ── Bridge Protocol Types ──────────────────────────────────────────

export interface BridgeMessage {
  type: 'command' | 'event' | 'response';
  channel: 'flow' | 'engine' | 'platform' | 'inspector' | 'registry';
  action: string;
  requestId: string;
  payload: any;
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
  defaultValue?: any;
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
  defaultValue?: any;
}

export interface FlowNodeData extends Record<string, unknown> {
  typeId: string;
  displayName: string;
  category: NodeCategory;
  color: string;
  inputPorts: PortDefinition[];
  outputPorts: PortDefinition[];
  properties: PropertyDefinition[];
  propertyValues: Record<string, any>;
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
  updatedAt: string;
}

// ── Runtime Types ─────────────────────────────────────────────────

export type NodeStatus = 'Idle' | 'Running' | 'Completed' | 'Error' | 'Skipped';

export interface LogEntry {
  timestamp: string;
  level: 'info' | 'warning' | 'error' | 'debug';
  message: string;
  nodeId?: string;
}

export type InspectorMode = 'none' | 'mira' | 'snip';
