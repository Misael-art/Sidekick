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
  nodeDisabled?: boolean;
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

export type SecuritySeverity = 'Info' | 'Warning' | 'Block' | 'info' | 'warning' | 'block';

export interface SecurityIssue {
  code: string;
  severity: SecuritySeverity;
  message: string;
  nodeId?: string;
}

export interface SecurityReport {
  isSafeToRun: boolean;
  issues: SecurityIssue[];
  riskLevel: 'low' | 'medium' | 'high' | string;
  manifestHash: string;
}

export interface SecurityAck {
  manifestHash: string;
  acceptedAt?: string;
}

export interface SecuritySettings {
  allowHighRiskExecution: boolean;
}

export type FlowHealthSeverity = 'Info' | 'Warning' | 'Error' | 'info' | 'warning' | 'error';

export interface FlowHealthIssue {
  code: string;
  severity: FlowHealthSeverity;
  message: string;
  nodeId?: string | null;
  propertyId?: string | null;
  action?: string | null;
}

export interface FlowHealthSuggestion {
  id: string;
  title: string;
  detail: string;
  action: string;
  priority: 'high' | 'medium' | 'low' | string;
  nodeId?: string | null;
}

export interface FlowHealthReport {
  score: number;
  level: string;
  canRunWithoutAttention: boolean;
  generatedAt?: string;
  issues: FlowHealthIssue[];
  suggestions: FlowHealthSuggestion[];
}

export type DryRunStepStatus = 'Ready' | 'Warning' | 'Blocked' | 'ready' | 'warning' | 'blocked';

export interface DryRunNodeStep {
  nodeId: string;
  typeId: string;
  displayName: string;
  status: DryRunStepStatus;
  requiresConfirmation: boolean;
  isDestructive?: boolean;
  message?: string;
}

export interface DryRunCheckpoint {
  kind: string;
  message: string;
  nodeId?: string | null;
}

export interface FlowDryRunReport {
  canRun: boolean;
  summary: string;
  validation: FlowValidationResult;
  security: SecurityReport;
  health: FlowHealthReport;
  steps: DryRunNodeStep[];
  checkpoints: DryRunCheckpoint[];
}

export interface RunnableFlowSummary {
  flowId: string;
  name: string;
  category: string;
  riskLevel: 'low' | 'medium' | 'high' | string;
  isPortfolio: boolean;
  requiresLocalConfirmation: boolean;
}

export interface FlowInvocationRequest {
  flowId: string;
  source: string;
  requestedBy: string;
  allowHighRisk: boolean;
  correlationId?: string;
  currentFlowId?: string | null;
  allowedFlowIds?: string[];
}

export type FlowInvocationStatus =
  | 'queued'
  | 'blocked'
  | 'needsConfiguration'
  | 'requiresLocalConfirmation'
  | 'notFound'
  | 'invalid'
  | 'unavailable';

export interface FlowInvocationResult {
  status: FlowInvocationStatus | string;
  flowId: string;
  flowName: string;
  message: string;
  validation?: FlowValidationResult | null;
  security?: SecurityReport | null;
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

export interface BrowsePathResponse {
  path?: string | null;
  cancelled?: boolean;
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
  fallback?: {
    useRelativeFallback?: boolean;
    useScaledFallback?: boolean;
    useAbsoluteFallback?: boolean;
    restoreWindowBeforeFallback?: boolean;
    expectedWindowState?: string;
    relativeX?: number;
    relativeY?: number;
    normalizedX?: number;
    normalizedY?: number;
    absoluteX?: number;
    absoluteY?: number;
  };
}

export interface InspectionAssetContent {
  name?: string | null;
  automationId?: string | null;
  className?: string | null;
  controlType?: string | null;
  cursorPixelColor?: string | null;
  thumbnailPath?: string | null;
  thumbnailBase64?: string | null;
  detectedText?: string | null;
  currentText?: string | null;
  placeholderText?: string | null;
  textSource?: string | null;
  captureQuality?: string | null;
  valueText?: string | null;
  textPatternText?: string | null;
  legacyName?: string | null;
  legacyValue?: string | null;
  helpText?: string | null;
  ocrAttempted?: boolean;
  ocrAvailable?: boolean;
  ocrText?: string | null;
  ocrWarning?: string | null;
  cursorX?: number;
  cursorY?: number;
  isFocused?: boolean;
  isEnabled?: boolean;
  isVisible?: boolean;
  hostScreenWidth: number;
  hostScreenHeight: number;
  windowStateAtCapture?: string;
  windowHandle?: number | null;
  monitorDeviceName?: string | null;
  monitorBounds?: InspectionAssetBounds;
  dpiScale?: number;
  relativePointX?: number;
  relativePointY?: number;
  normalizedWindowX?: number;
  normalizedWindowY?: number;
  normalizedScreenX?: number;
  normalizedScreenY?: number;
}

export interface InspectionAsset {
  id: string;
  kind: 'inspection';
  schemaVersion?: number;
  version: number;
  createdAt: string;
  updatedAt: string;
  displayName: string;
  tags: string[];
  notes?: string | null;
  source: InspectionAssetSourceInfo;
  locator: InspectionAssetLocator;
  content: InspectionAssetContent;
  browser?: {
    isBrowserSurface?: boolean;
    url?: string | null;
    origin?: string | null;
    documentTitle?: string | null;
    captureHint?: string | null;
    recommendedNodes?: string[];
  } | null;
}

export interface CapturedElement {
  automationId: string;
  name: string;
  valueText?: string;
  textPatternText?: string;
  legacyName?: string;
  legacyValue?: string;
  helpText?: string;
  detectedText?: string;
  currentText?: string;
  placeholderText?: string;
  textSource?: string;
  captureQuality?: string;
  ocrAttempted?: boolean;
  ocrAvailable?: boolean;
  ocrText?: string;
  ocrWarning?: string;
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
  windowStateAtCapture?: string;
  windowHandle?: number | null;
  monitorDeviceName?: string;
  monitorBounds?: InspectionAssetBounds;
  hostScreenWidth?: number;
  hostScreenHeight?: number;
  dpiScale?: number;
  relativePointX?: number;
  relativePointY?: number;
  normalizedWindowX?: number;
  normalizedWindowY?: number;
  normalizedScreenX?: number;
  normalizedScreenY?: number;
  isBrowserSurface?: boolean;
  browserUrl?: string;
  browserOrigin?: string;
  browserDocumentTitle?: string;
  browserCaptureHint?: string;
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

export interface SelectorDiagnosticResult {
  testedAt?: string;
  found: boolean;
  strength: 'forte' | 'media' | 'fraca' | 'inexistente' | string;
  reason: string;
  fallbackRecommendation: string;
  repairAction?: 'repairWithLatestCapture' | 'none' | string;
  bounds?: InspectionAssetBounds | null;
  selector?: {
    windowTitle?: string | null;
    automationId?: string | null;
    name?: string | null;
    controlType?: string | null;
    processName?: string | null;
    processPath?: string | null;
    titleMatch?: string | null;
  };
}

export interface CapturedRegion {
  image: string;
  bounds: SnipAssetBounds;
  asset?: SnipAsset | null;
  assetSaveError?: string | null;
}

export interface MacroRecorderOptions {
  captureMouse?: boolean;
  captureKeyboard?: boolean;
  captureText?: boolean;
  captureSensitiveText?: boolean;
  stopHotkey?: string;
  targetProcessName?: string | null;
  maxEvents?: number;
  idlePauseMs?: number;
  goal?: string | null;
}

export interface MacroRecordingSession {
  sessionId: string;
  startedAt: string;
  stoppedAt?: string | null;
  status: 'idle' | 'recording' | 'stopped' | 'cancelled' | string;
  eventCount: number;
  privacyMode: string;
  goal?: string | null;
}

export interface RecorderWindowContext {
  windowTitle?: string | null;
  processName?: string | null;
  processPath?: string | null;
  processId?: number;
  windowHandle?: number;
}

export interface RecorderElementContext {
  automationId?: string | null;
  name?: string | null;
  className?: string | null;
  controlType?: string | null;
  windowTitle?: string | null;
  processName?: string | null;
  processPath?: string | null;
  processId?: number;
  bounds?: InspectionAssetBounds | null;
  windowBounds?: InspectionAssetBounds | null;
  relativeX?: number;
  relativeY?: number;
  normalizedX?: number;
  normalizedY?: number;
  absoluteX?: number;
  absoluteY?: number;
  cursorPixelColor?: string | null;
  detectedText?: string | null;
  currentText?: string | null;
  placeholderText?: string | null;
  selectorStrength?: string;
  selectorStrategy?: string;
  isBrowserSurface?: boolean;
  browserUrl?: string | null;
  browserOrigin?: string | null;
  browserDocumentTitle?: string | null;
}

export interface RecorderMousePayload {
  x?: number;
  y?: number;
  startX?: number;
  startY?: number;
  endX?: number;
  endY?: number;
  delta?: number;
  button?: string;
}

export interface RecorderKeyboardPayload {
  key?: string;
  text?: string;
  modifiers?: string[];
}

export interface RecorderTextPayload {
  value?: string | null;
  length: number;
  isRedacted?: boolean;
}

export interface RecorderPrivacyInfo {
  isRedacted: boolean;
  mode: string;
  reason?: string | null;
}

export interface RecorderEvent {
  id: string;
  kind: string;
  timestamp: string;
  label?: string;
  window?: RecorderWindowContext | null;
  element?: RecorderElementContext | Partial<CapturedElement> | null;
  mouse?: RecorderMousePayload | null;
  keyboard?: RecorderKeyboardPayload | null;
  text?: RecorderTextPayload | null;
  privacy?: RecorderPrivacyInfo;
  confidence?: number;
  warnings?: string[];
}

export interface RecorderSuggestedNode {
  id: string;
  typeId: string;
  position: { x: number; y: number };
  properties: Record<string, unknown>;
  confidence?: number;
  warnings?: string[];
}

export interface RecorderSuggestedConnection {
  id: string;
  sourceNodeId: string;
  sourcePort: string;
  targetNodeId: string;
  targetPort: string;
}

export interface GuidedAutomationDraft {
  id: string;
  sessionId?: string;
  displayName: string;
  isDraft: boolean;
  startedAt?: string;
  stoppedAt?: string;
  events: RecorderEvent[];
  suggestedNodes?: RecorderSuggestedNode[];
  suggestedConnections?: RecorderSuggestedConnection[];
  savedInspectionAsset?: InspectionAsset | null;
  warnings?: string[];
  limitations?: string[];
  score?: number;
}

// ── Flow Runtime Types ───────────────────────────────────────────

export type StopFlowMode = 'currentOnly' | 'cancelAll';

export interface StopFlowResult {
  cancelledCurrentRun: boolean;
  clearedQueuedRuns: number;
  remainingQueueLength: number;
  isRunning: boolean;
}

export interface ClearQueueResult {
  clearedQueuedRuns: number;
  remainingQueueLength: number;
  isRunning: boolean;
}

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
