// ── Bridge Protocol Types ──────────────────────────────────────────

export interface BridgeMessage {
  type: 'command' | 'event' | 'response';
  channel: 'flow' | 'engine' | 'platform' | 'inspector' | 'registry' | 'assets';
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
  nodeAlias?: string;
  nodeComment?: string;
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

export type ValidationSeverity = 'error' | 'warning';

export interface ValidationIssue {
  severity: ValidationSeverity;
  code: string;
  message: string;
  nodeId?: string;
  connectionId?: string;
  propertyId?: string;
}

export interface FlowValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  issues: ValidationIssue[];
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

export interface SnipAssetBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface SnipAssetSourceInfo {
  processName?: string | null;
  processId?: number | null;
  windowTitle?: string | null;
  windowClassName?: string | null;
}

export interface SnipAssetContent {
  imagePath: string;
  ocrText?: string | null;
  ocrConfidence?: number | null;
}

export interface SnipAsset {
  id: string;
  kind: 'snip';
  version: number;
  createdAt: string;
  updatedAt: string;
  displayName: string;
  tags: string[];
  notes?: string | null;
  source: SnipAssetSourceInfo;
  captureBounds: SnipAssetBounds;
  content: SnipAssetContent;
}

export interface SnipAssetTemplatePayload {
  assetId: string;
  displayName: string;
  imagePath: string;
  imageBase64: string;
}

export interface ImageTemplateValue {
  kind: 'snipAsset' | 'inline';
  imageBase64: string;
  assetId?: string;
  displayName?: string;
  imagePath?: string;
}

export interface InspectionAssetBounds {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface InspectionAssetSourceInfo {
  processName?: string | null;
  processPath?: string | null;
  processId?: number | null;
  windowTitle?: string | null;
}

export interface InspectionAssetSelector {
  windowTitle?: string | null;
  automationId?: string | null;
  name?: string | null;
  className?: string | null;
  controlType?: string | null;
}

export interface InspectionAssetLocator {
  strategy: 'selectorPreferred' | 'relativePositionFallback' | 'absolutePositionLastResort' | string;
  strength?: 'forte' | 'media' | 'fraca' | string;
  strengthReason?: string | null;
  selector: InspectionAssetSelector;
  relativeBounds: InspectionAssetBounds;
  absoluteBounds: InspectionAssetBounds;
}

export interface InspectionAssetContent {
  name?: string | null;
  automationId?: string | null;
  className?: string | null;
  controlType?: string | null;
  cursorPixelColor?: string | null;
  cursorX?: number;
  cursorY?: number;
  isFocused?: boolean;
  isEnabled?: boolean;
  isVisible?: boolean;
  hostScreenWidth: number;
  hostScreenHeight: number;
}

export interface InspectionAsset {
  id: string;
  kind: 'inspection';
  version: number;
  createdAt: string;
  updatedAt: string;
  displayName: string;
  tags: string[];
  notes?: string | null;
  source: InspectionAssetSourceInfo;
  locator: InspectionAssetLocator;
  content: InspectionAssetContent;
}

export interface CapturedElement {
  automationId: string;
  name: string;
  className: string;
  controlType: string;
  boundingRect: InspectionAssetBounds;
  windowBounds?: InspectionAssetBounds;
  relativeBoundingRect?: InspectionAssetBounds;
  processId: number;
  processName?: string;
  processPath?: string;
  windowTitle: string;
  cursorScreen?: { x: number; y: number };
  cursorPixelColor?: string | null;
  isFocused?: boolean;
  isEnabled?: boolean;
  isVisible?: boolean;
  selectorStrength?: string;
  selectorStrategy?: string;
  asset?: InspectionAsset | null;
  assetSaveError?: string | null;
}

export interface InspectionAssetTestResult {
  found: boolean;
  bounds?: InspectionAssetBounds | null;
  strategy?: string;
  strength?: string;
}

export interface CapturedRegion {
  image: string;
  bounds: SnipAssetBounds;
  asset?: SnipAsset | null;
  assetSaveError?: string | null;
}

// ── Flow Runtime Types ───────────────────────────────────────────

export type StopFlowMode = 'currentOnly' | 'cancelAll';

export interface CurrentRunSnapshot {
  flowId: string;
  flowName: string;
  source: string;
  triggerNodeId?: string;
  startedAt: string;
}

export interface FlowRuntimeSnapshot {
  flowId: string;
  flowName: string;
  state: 'queued' | 'running' | 'armed' | 'error' | 'inactive';
  isArmed: boolean;
  isRunning: boolean;
  queuePending: boolean;
  activeTriggerNodeIds: string[];
  lastRunAt?: string;
  lastTriggerAt?: string;
  lastError?: string;
}

export interface RuntimeStatusSnapshot {
  isRunning: boolean;
  queueLength: number;
  armedFlowCount: number;
  currentRun: CurrentRunSnapshot | null;
  flows: FlowRuntimeSnapshot[];
}

export interface FlowQueuedEvent {
  flowId: string;
  flowName: string;
  source: string;
}

export interface TriggerRuntimeEvent {
  triggerNodeId: string;
  flowId: string;
  flowName: string;
}

export interface RuntimeErrorEvent {
  flowId?: string;
  error: string;
}

export interface RuntimePhaseEvent {
  flowId: string;
  flowName: string;
  nodeId: string;
  phase: string;
  message?: string | null;
  detail?: unknown;
  timestamp: string;
}

export type FlowExecutionResult = 'running' | 'completed' | 'error' | 'cancelled';

export interface ExecutionHistoryLogEntry {
  timestamp: string;
  level: 'info' | 'warning' | 'error' | 'phase';
  nodeId?: string;
  message: string;
}

export interface ExecutionNodeStatusEntry {
  timestamp: string;
  nodeId: string;
  status: WireNodeStatus;
}

export interface FlowExecutionHistoryEntry {
  runId: string;
  flowId: string;
  flowName: string;
  source: 'manual' | 'trigger';
  triggerNodeId?: string;
  startedAt: string;
  finishedAt?: string;
  result: FlowExecutionResult;
  error?: string;
  logs: ExecutionHistoryLogEntry[];
  nodeStatuses: ExecutionNodeStatusEntry[];
}

export function normalizeFlowExecutionHistoryEntry(
  input: Partial<FlowExecutionHistoryEntry> | null | undefined,
): FlowExecutionHistoryEntry {
  return {
    runId: input?.runId ?? '',
    flowId: input?.flowId ?? '',
    flowName: input?.flowName ?? '',
    source: input?.source ?? 'manual',
    triggerNodeId: input?.triggerNodeId,
    startedAt: input?.startedAt ?? new Date(0).toISOString(),
    finishedAt: input?.finishedAt,
    result: input?.result ?? 'running',
    error: input?.error,
    logs: input?.logs ?? [],
    nodeStatuses: input?.nodeStatuses ?? [],
  };
}

export function normalizeFlowRuntimeSnapshot(
  input: Partial<FlowRuntimeSnapshot> | null | undefined,
): FlowRuntimeSnapshot {
  return {
    flowId: input?.flowId ?? '',
    flowName: input?.flowName ?? '',
    state: input?.state ?? 'inactive',
    isArmed: input?.isArmed ?? false,
    isRunning: input?.isRunning ?? false,
    queuePending: input?.queuePending ?? false,
    activeTriggerNodeIds: input?.activeTriggerNodeIds ?? [],
    lastRunAt: input?.lastRunAt,
    lastTriggerAt: input?.lastTriggerAt,
    lastError: input?.lastError,
  };
}

export function normalizeRuntimeStatusSnapshot(
  input: Partial<RuntimeStatusSnapshot> | null | undefined,
): RuntimeStatusSnapshot {
  return {
    isRunning: input?.isRunning ?? false,
    queueLength: input?.queueLength ?? 0,
    armedFlowCount: input?.armedFlowCount ?? 0,
    currentRun: input?.currentRun ?? null,
    flows: input?.flows ?? [],
  };
}
