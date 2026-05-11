import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { sendCommand } from '../../bridge/bridge';
import { toBackendFlow, type BackendFlow } from '../../bridge/flowConverter';
import type {
  CapturedElement,
  ClearQueueResult,
  FlowDryRunReport,
  FlowHealthReport,
  FlowValidationResult,
  FlowNodeData,
  FlowRuntimeSnapshot,
  GuidedAutomationDraft,
  InspectionAsset,
  InspectionAssetTestResult,
  MacroRecordingSession,
  SecurityReport,
  StopFlowResult,
  StopFlowMode,
} from '../../bridge/types';
import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';
import { useLocaleStore, useTranslation } from '../../i18n';

interface FlowSummary {
  id: string;
  name?: string;
  modifiedAt?: string;
  nodeCount?: number;
  isNative?: boolean;
  preflightStatus?: 'ready' | 'needsConfiguration' | 'blocked' | string;
  preflightMessage?: string;
}

interface RecipeCatalogEntry {
  id: string;
  name: string;
  category: string;
  persona: string;
  risk: 'low' | 'medium' | 'high' | string;
  popularity: number;
  tags: string[];
}

interface FlowActivationResponse {
  armed?: boolean;
  flow?: FlowRuntimeSnapshot;
  warnings?: string[];
  validation?: FlowValidationResult;
  security?: SecurityReport;
}

interface RunFlowResponse {
  queued?: boolean;
  restarted?: boolean;
  flowId?: string;
  queueLength?: number;
  queuePending?: boolean;
  cancelledCurrentRun?: boolean;
  clearedQueuedRuns?: number;
  remainingQueueLength?: number;
  validation?: FlowValidationResult;
  security?: SecurityReport;
}

function getErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message.trim()) {
    return error.message;
  }

  return fallback;
}

function formatModifiedAt(value?: string): string {
  if (!value) {
    return 'Data indisponivel';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString('pt-BR');
}

function confirmDiscardUnsavedChanges(nextActionLabel: string): boolean {
  return window.confirm(
    `Existem alteracoes nao salvas no fluxo atual. Deseja descartalas e ${nextActionLabel}?`,
  );
}

function normalizeSearchText(value?: string): string {
  return value?.trim().toLocaleLowerCase('pt-BR') ?? '';
}

function isDemoFlow(flow: FlowSummary): boolean {
  const haystack = `${flow.id} ${flow.name ?? ''}`.toLocaleLowerCase('pt-BR');
  return haystack.includes('demo');
}

function formatBounds(bounds?: { x: number; y: number; width: number; height: number } | null): string {
  if (!bounds) {
    return 'Posicao indisponivel';
  }

  return `${bounds.x}, ${bounds.y} • ${bounds.width}x${bounds.height}`;
}

function formatInspectionCapturedAt(value?: string): string {
  if (!value) {
    return 'Data indisponivel';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString('pt-BR');
}

function buildInspectionSearchText(asset: InspectionAsset): string {
  return normalizeSearchText([
    asset.displayName,
    asset.content.name,
    asset.content.detectedText,
    asset.content.currentText,
    asset.content.placeholderText,
    asset.content.textSource,
    asset.content.automationId,
    asset.content.className,
    asset.content.controlType,
    asset.source.windowTitle,
    asset.source.processName,
    asset.source.processPath,
    asset.locator.strength,
    asset.locator.strategy,
    ...asset.tags,
  ].filter(Boolean).join(' '));
}

function buildInspectionSummary(asset: InspectionAsset): string {
  const summaryParts = [
    asset.content.controlType,
    asset.content.name,
    asset.content.detectedText,
    asset.content.automationId,
    asset.locator.strength,
    asset.locator.strategy,
  ].filter(Boolean);

  return summaryParts.length > 0 ? summaryParts.join(' • ') : 'Metadados basicos indisponiveis';
}

function hasContinuousTriggers(nodes: Array<{ data: FlowNodeData }>): boolean {
  return nodes.some((node) => node.data.category === 'Trigger' && node.data.typeId !== 'trigger.manualStart');
}

function buildStopResultMessage(mode: StopFlowMode, result: StopFlowResult | null | undefined): string {
  const cleared = result?.clearedQueuedRuns ?? 0;
  if (mode === 'cancelAll') {
    return result?.cancelledCurrentRun
      ? `Execucao atual interrompida e ${cleared} item(ns) removido(s) da fila.`
      : `${cleared} item(ns) removido(s) da fila.`;
  }

  return result?.cancelledCurrentRun
    ? 'Execucao atual interrompida. A fila pendente foi preservada.'
    : 'Nenhuma execucao ativa para interromper.';
}

function summarizeIssues(messages: string[], maxItems = 2): string {
  if (messages.length === 0) {
    return '';
  }

  const visible = messages.slice(0, maxItems).join(' | ');
  const remaining = messages.length - maxItems;
  return remaining > 0 ? `${visible} | +${remaining} restante(s)` : visible;
}

function buildValidationErrorMessage(validation: FlowValidationResult): string {
  const details = summarizeIssues(validation.errors);
  return details
    ? `Corrija ${validation.errors.length} erro(s) antes de continuar: ${details}`
    : 'Corrija os erros de validacao antes de continuar.';
}

function buildValidationWarningMessage(validation: FlowValidationResult, successLabel: string): string {
  const details = summarizeIssues(validation.warnings);
  return details
    ? `${successLabel} com ${validation.warnings.length} aviso(s): ${details}`
    : successLabel;
}

function isSecuritySeverity(issue: { severity?: string }, expected: 'block' | 'warning' | 'info'): boolean {
  return (issue.severity ?? '').toLocaleLowerCase('en-US') === expected;
}

function buildSecurityBlockMessage(security: SecurityReport): string {
  const blocks = security.issues.filter((issue) => isSecuritySeverity(issue, 'block'));
  const warnings = security.issues.filter((issue) => isSecuritySeverity(issue, 'warning'));
  if (blocks.length > 0) {
    return `Execucao bloqueada por seguranca (${security.riskLevel}): ${summarizeIssues(blocks.map((item) => item.message), 2)}`;
  }
  if (warnings.length > 0) {
    return `Atencao: fluxo com risco ${security.riskLevel}. ${summarizeIssues(warnings.map((item) => item.message), 2)}`;
  }
  return `Fluxo com risco ${security.riskLevel}.`;
}

function withSecurityAckIfNeeded(
  flowPayload: BackendFlow,
  security: SecurityReport,
  acknowledged: boolean,
): BackendFlow & { securityAck?: { manifestHash: string } } {
  if (!acknowledged || security.isSafeToRun) {
    return flowPayload;
  }

  return { ...flowPayload, securityAck: { manifestHash: security.manifestHash } };
}

function isElevatedCatalogRisk(risk: string | undefined): boolean {
  const normalized = (risk ?? '').toLowerCase();
  return normalized === 'medium' || normalized === 'high';
}

function getPreflightLabel(flow: FlowSummary): string {
  switch (flow.preflightStatus) {
    case 'ready':
      return 'Pronto';
    case 'needsConfiguration':
      return 'Precisa configurar';
    case 'blocked':
      return 'Bloqueado';
    default:
      return 'Preflight';
  }
}

export default function Toolbar() {
  const { t, locale } = useTranslation();
  const setLocale = useLocaleStore((s) => s.setLocale);
  const addStickyNote = useFlowStore((s) => s.addStickyNote);
  const flowName = useFlowStore((s) => s.flowName);
  const flowId = useFlowStore((s) => s.flowId);
  const isDirty = useFlowStore((s) => s.isDirty);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const stickyNotes = useFlowStore((s) => s.stickyNotes);
  const flowVariables = useFlowStore((s) => s.flowVariables);
  const setFlowName = useFlowStore((s) => s.setFlowName);
  const saveFlow = useFlowStore((s) => s.saveFlow);
  const newFlow = useFlowStore((s) => s.newFlow);
  const loadFlow = useFlowStore((s) => s.loadFlow);
  const validateFlow = useFlowStore((s) => s.validateFlow);

  const isRunning = useAppStore((s) => s.isRunning);
  const queueLength = useAppStore((s) => s.queueLength);
  const currentRun = useAppStore((s) => s.currentRun);
  const flowRuntimes = useAppStore((s) => s.flowRuntimes);
  const clearNodeStatuses = useAppStore((s) => s.clearNodeStatuses);
  const inspectorMode = useAppStore((s) => s.inspectorMode);
  const setInspectorMode = useAppStore((s) => s.setInspectorMode);
  const capturedElement = useAppStore((s) => s.capturedElement);
  const inspectionAssets = useAppStore((s) => s.inspectionAssets);
  const addLog = useAppStore((s) => s.addLog);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const debugVisualEnabled = useAppStore((s) => s.debugVisualEnabled);
  const setDebugVisualEnabled = useAppStore((s) => s.setDebugVisualEnabled);
  const allowHighRiskExecution = useAppStore((s) => s.allowHighRiskExecution);
  const setAllowHighRiskExecution = useAppStore((s) => s.setAllowHighRiskExecution);
  const flowHealthReport = useAppStore((s) => s.flowHealthReport);
  const setFlowHealthReport = useAppStore((s) => s.setFlowHealthReport);
  const dryRunReport = useAppStore((s) => s.dryRunReport);
  const setDryRunReport = useAppStore((s) => s.setDryRunReport);
  const macroRecorderActive = useAppStore((s) => s.macroRecorderActive);
  const setMacroRecorderActive = useAppStore((s) => s.setMacroRecorderActive);
  const setMacroRecorderStatus = useAppStore((s) => s.setMacroRecorderStatus);
  const setGuidedDraft = useAppStore((s) => s.setGuidedDraft);

  const highRiskResolveRef = useRef<((ok: boolean) => void) | null>(null);
  const [highRiskDialogSecurity, setHighRiskDialogSecurity] = useState<SecurityReport | null>(null);

  const [isEditing, setIsEditing] = useState(false);
  const [editName, setEditName] = useState(flowName);
  const [availableFlows, setAvailableFlows] = useState<FlowSummary[]>([]);
  const [isLoadDialogOpen, setIsLoadDialogOpen] = useState(false);
  const [selectedFlowId, setSelectedFlowId] = useState<string | null>(null);
  const [loadFilter, setLoadFilter] = useState('');
  const [isMarketplaceOpen, setIsMarketplaceOpen] = useState(false);
  const [marketplaceFilter, setMarketplaceFilter] = useState('');
  const [selectedMarketplaceFlowId, setSelectedMarketplaceFlowId] = useState<string | null>(null);
  const [isLoadingFlowList, setIsLoadingFlowList] = useState(false);
  const [recipeCatalog, setRecipeCatalog] = useState<RecipeCatalogEntry[]>([]);
  const [isApplyingLoadedFlow, setIsApplyingLoadedFlow] = useState(false);
  const [deletingFlowId, setDeletingFlowId] = useState<string | null>(null);
  const [isStopDialogOpen, setIsStopDialogOpen] = useState(false);
  const [isStoppingFlow, setIsStoppingFlow] = useState(false);
  const [isClearingQueue, setIsClearingQueue] = useState(false);
  const [isRestartingFlow, setIsRestartingFlow] = useState(false);
  const [isDryRunDialogOpen, setIsDryRunDialogOpen] = useState(false);
  const [isRunningDryRun, setIsRunningDryRun] = useState(false);
  const [isAnalyzingHealth, setIsAnalyzingHealth] = useState(false);
  const [isKillSwitching, setIsKillSwitching] = useState(false);
  const [isMacroRecorderBusy, setIsMacroRecorderBusy] = useState(false);
  const [isExportingRunner, setIsExportingRunner] = useState(false);
  const [isInspectionLibraryOpen, setIsInspectionLibraryOpen] = useState(false);
  const [inspectionFilter, setInspectionFilter] = useState('');
  const [selectedInspectionAssetId, setSelectedInspectionAssetId] = useState<string | null>(null);
  const [inspectionAssetBusyId, setInspectionAssetBusyId] = useState<string | null>(null);
  const [inspectionAssetTestResult, setInspectionAssetTestResult] = useState<string>('');
  const [inspectionAssetNameDraft, setInspectionAssetNameDraft] = useState('');
  const [inspectionAssetNotesDraft, setInspectionAssetNotesDraft] = useState('');
  const [inspectionAssetTagsDraft, setInspectionAssetTagsDraft] = useState('');
  const inputRef = useRef<HTMLInputElement>(null);
  const loadSearchInputRef = useRef<HTMLInputElement>(null);
  const marketplaceSearchInputRef = useRef<HTMLInputElement>(null);
  const inspectionSearchInputRef = useRef<HTMLInputElement>(null);

  const waitForHighRiskConfirmation = (security: SecurityReport) =>
    new Promise<boolean>((resolve) => {
      highRiskResolveRef.current = resolve;
      setHighRiskDialogSecurity(security);
    });

  const resolveHighRiskDialog = (ok: boolean) => {
    setHighRiskDialogSecurity(null);
    const fn = highRiskResolveRef.current;
    highRiskResolveRef.current = null;
    fn?.(ok);
  };

  const handleToggleHighRiskPermission = async () => {
    try {
      setUserMessage(null);
      const next = !allowHighRiskExecution;
      await setAllowHighRiskExecution(next);
      setUserMessage({
        type: 'info',
        text: next
          ? 'Permissao ativada: fluxos com bloqueio de seguranca exigem confirmacao no modal antes de executar, armar ou exportar.'
          : 'Permissao desativada: fluxos com bloqueio de seguranca voltam a ser recusados.',
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel atualizar as definicoes de seguranca.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const filteredFlows = useMemo(() => {
    const normalizedFilter = normalizeSearchText(loadFilter);
    if (!normalizedFilter) {
      return availableFlows;
    }

    return availableFlows.filter((flow) => {
      const normalizedName = normalizeSearchText(flow.name);
      const normalizedId = normalizeSearchText(flow.id);
      return normalizedName.includes(normalizedFilter) || normalizedId.includes(normalizedFilter);
    });
  }, [availableFlows, loadFilter]);

  const marketplaceFlows = useMemo(() => {
    const normalizedFilter = normalizeSearchText(marketplaceFilter);
    const byId = new Map(recipeCatalog.map((entry) => [entry.id, entry]));
    return availableFlows
      .filter((flow) => {
        const haystack = normalizeSearchText(`${flow.id} ${flow.name ?? ''}`);
        const catalog = byId.get(flow.id);
        const catalogTags = normalizeSearchText(catalog?.tags?.join(' ') ?? '');
        const isRecipe = haystack.includes('recipe')
          || haystack.includes('trae')
          || haystack.includes('portfolio')
          || Boolean(flow.isNative)
          || Boolean(catalog);

        return isRecipe && (!normalizedFilter || haystack.includes(normalizedFilter) || catalogTags.includes(normalizedFilter));
      })
      .sort((left, right) => {
        const leftPopularity = byId.get(left.id)?.popularity ?? 0;
        const rightPopularity = byId.get(right.id)?.popularity ?? 0;
        if (leftPopularity !== rightPopularity) {
          return rightPopularity - leftPopularity;
        }
        return (left.name ?? left.id).localeCompare(right.name ?? right.id, 'pt-BR');
      });
  }, [availableFlows, marketplaceFilter, recipeCatalog]);

  const filteredInspectionAssets = useMemo(() => {
    const normalizedFilter = normalizeSearchText(inspectionFilter);
    if (!normalizedFilter) {
      return inspectionAssets;
    }

    return inspectionAssets.filter((asset) => buildInspectionSearchText(asset).includes(normalizedFilter));
  }, [inspectionAssets, inspectionFilter]);

  const selectedInspectionAsset = useMemo(() => (
    filteredInspectionAssets.find((asset) => asset.id === selectedInspectionAssetId)
      ?? filteredInspectionAssets[0]
      ?? null
  ), [filteredInspectionAssets, selectedInspectionAssetId]);

  const currentFlowRuntime = flowRuntimes[flowId] ?? null;
  const isCurrentFlowArmed = Boolean(currentFlowRuntime?.isArmed);
  const isCurrentFlowRunning = currentRun?.flowId === flowId || Boolean(currentFlowRuntime?.isRunning);
  const isCurrentFlowQueued = Boolean(currentFlowRuntime?.queuePending);
  const canArmCurrentFlow = hasContinuousTriggers(nodes);
  const canStop = isRunning || queueLength > 0;
  const canClearQueue = queueLength > 0;
  const canRestartCurrentFlow = isCurrentFlowRunning || isCurrentFlowQueued;
  const canKillSwitch = canStop || Object.values(flowRuntimes).some((flow) => flow.isArmed || flow.queuePending);

  useEffect(() => {
    if (isEditing && inputRef.current) {
      inputRef.current.focus();
      inputRef.current.select();
    }
  }, [isEditing]);

  useEffect(() => {
    if (!isLoadDialogOpen) {
      return;
    }

    loadSearchInputRef.current?.focus();
    loadSearchInputRef.current?.select();

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape' && !isApplyingLoadedFlow) {
        setIsLoadDialogOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isLoadDialogOpen, isApplyingLoadedFlow]);

  useEffect(() => {
    if (!isMarketplaceOpen) {
      return;
    }

    marketplaceSearchInputRef.current?.focus();
    marketplaceSearchInputRef.current?.select();

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape' && !isApplyingLoadedFlow) {
        setIsMarketplaceOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isMarketplaceOpen, isApplyingLoadedFlow]);

  useEffect(() => {
    if (!isStopDialogOpen) {
      return;
    }

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape' && !isStoppingFlow) {
        setIsStopDialogOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isStopDialogOpen, isStoppingFlow]);

  useEffect(() => {
    if (!isInspectionLibraryOpen) {
      return;
    }

    inspectionSearchInputRef.current?.focus();
    inspectionSearchInputRef.current?.select();

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsInspectionLibraryOpen(false);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [isInspectionLibraryOpen]);

  useEffect(() => {
    if (!isLoadDialogOpen) {
      return;
    }

    if (filteredFlows.length === 0) {
      setSelectedFlowId(null);
      return;
    }

    if (!selectedFlowId || !filteredFlows.some((flow) => flow.id === selectedFlowId)) {
      setSelectedFlowId(filteredFlows[0].id);
    }
  }, [filteredFlows, isLoadDialogOpen, selectedFlowId]);

  useEffect(() => {
    if (!isMarketplaceOpen) {
      return;
    }

    if (marketplaceFlows.length === 0) {
      setSelectedMarketplaceFlowId(null);
      return;
    }

    if (!selectedMarketplaceFlowId || !marketplaceFlows.some((flow) => flow.id === selectedMarketplaceFlowId)) {
      setSelectedMarketplaceFlowId(marketplaceFlows[0].id);
    }
  }, [isMarketplaceOpen, marketplaceFlows, selectedMarketplaceFlowId]);

  useEffect(() => {
    if (queueLength === 0 && !isRunning) {
      setIsStopDialogOpen(false);
      setIsStoppingFlow(false);
    }
  }, [isRunning, queueLength]);

  useEffect(() => {
    if (!isInspectionLibraryOpen) {
      return;
    }

    if (filteredInspectionAssets.length === 0) {
      setSelectedInspectionAssetId(null);
      return;
    }

    if (!selectedInspectionAssetId || !filteredInspectionAssets.some((asset) => asset.id === selectedInspectionAssetId)) {
      setSelectedInspectionAssetId(filteredInspectionAssets[0].id);
    }
  }, [filteredInspectionAssets, isInspectionLibraryOpen, selectedInspectionAssetId]);

  useEffect(() => {
    setInspectionAssetNameDraft(selectedInspectionAsset?.displayName ?? '');
    setInspectionAssetNotesDraft(selectedInspectionAsset?.notes ?? '');
    setInspectionAssetTagsDraft((selectedInspectionAsset?.tags ?? []).join(', '));
  }, [selectedInspectionAsset?.id]);

  const commitName = () => {
    const trimmed = editName.trim();
    if (trimmed) {
      setFlowName(trimmed);
    }

    setIsEditing(false);
  };

  const handleNameKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      commitName();
    }

    if (event.key === 'Escape') {
      setEditName(flowName);
      setIsEditing(false);
    }
  };

  const handlePlay = async () => {
    try {
      setUserMessage(null);
      const securityFlow = toBackendFlow(flowId, flowName, nodes, edges, { runtimeView: true, variables: flowVariables });
      const security = await sendCommand<SecurityReport>('engine', 'securityLint', securityFlow);
      let securityAcknowledged = false;
      if (!security.isSafeToRun) {
        if (!allowHighRiskExecution) {
          const message = buildSecurityBlockMessage(security);
          addLog({ timestamp: new Date().toISOString(), level: 'error', message });
          setUserMessage({ type: 'error', text: message });
          return;
        }

        const confirmed = await waitForHighRiskConfirmation(security);
        if (!confirmed) {
          setUserMessage({ type: 'info', text: 'Execucao cancelada pelo utilizador.' });
          return;
        }

        securityAcknowledged = true;
      }

      const validation = await validateFlow();
      if (!validation.isValid) {
        const message = buildValidationErrorMessage(validation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      clearNodeStatuses();
      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges, {
        runtimeView: true,
        variables: flowVariables,
      });
      const runPayload = withSecurityAckIfNeeded(backendFlow, security, securityAcknowledged);
      const result = await sendCommand<RunFlowResponse>('engine', 'runFlow', runPayload);
      const runtimeValidation = result?.validation ?? validation;
      const runtimeSecurity = result?.security ?? security;

      if (!runtimeSecurity.isSafeToRun) {
        const message = buildSecurityBlockMessage(runtimeSecurity);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      if (!result?.queued) {
        const message = buildValidationErrorMessage(runtimeValidation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      setUserMessage({
        type: runtimeValidation.warnings.length > 0 ? 'info' : 'success',
        text: runtimeValidation.warnings.length > 0
          ? buildValidationWarningMessage(runtimeValidation, 'Execucao enfileirada com sucesso')
          : 'Execucao enfileirada com sucesso.',
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel enfileirar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleRestart = async () => {
    if (!canRestartCurrentFlow || isRestartingFlow) {
      return;
    }

    try {
      setIsRestartingFlow(true);
      setUserMessage(null);

      const securityFlow = toBackendFlow(flowId, flowName, nodes, edges, { runtimeView: true, variables: flowVariables });
      const security = await sendCommand<SecurityReport>('engine', 'securityLint', securityFlow);
      let securityAcknowledged = false;
      if (!security.isSafeToRun) {
        if (!allowHighRiskExecution) {
          const message = buildSecurityBlockMessage(security);
          addLog({ timestamp: new Date().toISOString(), level: 'error', message });
          setUserMessage({ type: 'error', text: message });
          return;
        }

        const confirmed = await waitForHighRiskConfirmation(security);
        if (!confirmed) {
          setUserMessage({ type: 'info', text: 'Reinicio cancelado pelo utilizador.' });
          return;
        }

        securityAcknowledged = true;
      }

      const validation = await validateFlow();
      if (!validation.isValid) {
        const message = buildValidationErrorMessage(validation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      clearNodeStatuses();
      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges, {
        runtimeView: true,
        variables: flowVariables,
      });
      const restartPayload = withSecurityAckIfNeeded(backendFlow, security, securityAcknowledged);
      const result = await sendCommand<RunFlowResponse>('engine', 'restartFlow', restartPayload);
      const runtimeValidation = result?.validation ?? validation;
      const runtimeSecurity = result?.security ?? security;

      if (!runtimeSecurity.isSafeToRun) {
        const message = buildSecurityBlockMessage(runtimeSecurity);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      if (!result?.queued) {
        const message = buildValidationErrorMessage(runtimeValidation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      setUserMessage({
        type: runtimeValidation.warnings.length > 0 ? 'info' : 'success',
        text: runtimeValidation.warnings.length > 0
          ? buildValidationWarningMessage(runtimeValidation, 'Flow reiniciado e enfileirado')
          : `Flow reiniciado: ${result.clearedQueuedRuns ?? 0} pendencia(s) antiga(s) removida(s).`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel reiniciar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsRestartingFlow(false);
    }
  };

  const handleArm = async () => {
    try {
      setUserMessage(null);
      const securityFlow = toBackendFlow(flowId, flowName, nodes, edges, { runtimeView: true, variables: flowVariables });
      const security = await sendCommand<SecurityReport>('engine', 'securityLint', securityFlow);
      let securityAcknowledged = false;
      if (!security.isSafeToRun) {
        if (!allowHighRiskExecution) {
          const message = buildSecurityBlockMessage(security);
          addLog({ timestamp: new Date().toISOString(), level: 'error', message });
          setUserMessage({ type: 'error', text: message });
          return;
        }

        const confirmed = await waitForHighRiskConfirmation(security);
        if (!confirmed) {
          setUserMessage({ type: 'info', text: 'Armamento cancelado pelo utilizador.' });
          return;
        }

        securityAcknowledged = true;
      }

      const validation = await validateFlow();
      if (!validation.isValid) {
        const message = buildValidationErrorMessage(validation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges, {
        runtimeView: true,
        variables: flowVariables,
      });
      const armPayload = withSecurityAckIfNeeded(backendFlow, security, securityAcknowledged);
      const result = await sendCommand<FlowActivationResponse>('engine', 'activateFlow', armPayload);
      const runtimeValidation = result?.validation ?? validation;
      const runtimeSecurity = result?.security ?? security;
      if (!runtimeSecurity.isSafeToRun) {
        const message = buildSecurityBlockMessage(runtimeSecurity);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }
      if (!result?.armed) {
        const message = buildValidationErrorMessage(runtimeValidation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      setUserMessage({
        type: runtimeValidation.warnings.length > 0 ? 'info' : 'success',
        text: runtimeValidation.warnings.length > 0
          ? buildValidationWarningMessage(runtimeValidation, 'Flow armado para monitoramento continuo')
          : 'Flow armado para monitoramento continuo.',
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel armar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleDisarm = async () => {
    try {
      setUserMessage(null);
      await sendCommand('engine', 'deactivateFlow', { flowId });
      setUserMessage({ type: 'success', text: 'Flow desarmado com sucesso.' });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel desarmar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const executeStop = async (mode: StopFlowMode) => {
    try {
      setIsStoppingFlow(true);
      setUserMessage(null);
      const result = await sendCommand<StopFlowResult>('engine', 'stopFlow', { mode });
      setIsStopDialogOpen(false);
      setUserMessage({
        type: 'info',
        text: buildStopResultMessage(mode, result),
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel interromper o runtime.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsStoppingFlow(false);
    }
  };

  const handleClearQueue = async () => {
    if (!canClearQueue || isClearingQueue) {
      return;
    }

    try {
      setIsClearingQueue(true);
      setUserMessage(null);
      const result = await sendCommand<ClearQueueResult>('engine', 'clearQueue', {});
      setUserMessage({
        type: 'info',
        text: `${result?.clearedQueuedRuns ?? 0} item(ns) removido(s) da fila. Execucao atual ${result?.isRunning ? 'continua em andamento' : 'nao esta ativa'}.`,
      });
      if ((result?.remainingQueueLength ?? 0) === 0) {
        setIsStopDialogOpen(false);
      }
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel limpar a fila do runtime.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsClearingQueue(false);
    }
  };

  const handleKillSwitch = async () => {
    if (!canKillSwitch) {
      return;
    }

    try {
      setIsKillSwitching(true);
      setUserMessage(null);
      await sendCommand('engine', 'killSwitch', {});
      setUserMessage({ type: 'info', text: 'Kill switch acionado: runtime parado, fila limpa e monitoramentos desarmados.' });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel acionar o kill switch.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsKillSwitching(false);
    }
  };

  const analyzeFlowExperience = async () => {
    try {
      setIsAnalyzingHealth(true);
      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges, {
        runtimeView: true,
        variables: flowVariables,
      });
      const report = await sendCommand<FlowHealthReport>('flow', 'analyzeFlowExperience', backendFlow);
      setFlowHealthReport(report);
      setUserMessage({
        type: report.score >= 70 ? 'success' : 'info',
        text: `Flow Health ${report.score}/100 (${report.level}). ${report.issues[0]?.message ?? 'Nenhum problema critico encontrado.'}`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel analisar a saude do flow.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsAnalyzingHealth(false);
    }
  };

  const runDryRun = async () => {
    try {
      setIsRunningDryRun(true);
      setUserMessage(null);
      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges, {
        runtimeView: true,
        variables: flowVariables,
      });
      const report = await sendCommand<FlowDryRunReport>('engine', 'dryRunFlow', backendFlow);
      setDryRunReport(report);
      setFlowHealthReport(report.health);
      setIsDryRunDialogOpen(true);
      setUserMessage({ type: report.canRun ? 'success' : 'info', text: report.summary });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel executar o dry-run.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsRunningDryRun(false);
    }
  };

  const handleStop = async () => {
    if (!canStop) {
      return;
    }

    if (queueLength > 0) {
      setIsStopDialogOpen(true);
      return;
    }

    await executeStop('currentOnly');
  };

  const handleNew = async () => {
    if (isDirty && !confirmDiscardUnsavedChanges('criar um novo fluxo')) {
      return;
    }

    try {
      setUserMessage(null);
      await newFlow();
      clearNodeStatuses();
      setUserMessage({ type: 'success', text: 'Novo fluxo criado com sucesso.' });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: 'Novo fluxo criado com sucesso.',
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel criar um novo fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleSave = async () => {
    try {
      setUserMessage(null);
      await saveFlow();
      setUserMessage({ type: 'success', text: 'Fluxo salvo com sucesso.' });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Fluxo "${flowName}" salvo com sucesso.`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel salvar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    }
  };

  const handleLoad = async () => {
    try {
      setUserMessage(null);
      setIsLoadingFlowList(true);
      const flows = await sendCommand<FlowSummary[]>('flow', 'listFlows', {});
      const catalog = await sendCommand<RecipeCatalogEntry[]>('flow', 'listRecipeCatalog', {});
      const normalizedFlows = Array.isArray(flows) ? flows : [];
      setRecipeCatalog(Array.isArray(catalog) ? catalog : []);
      setAvailableFlows(normalizedFlows);
      setLoadFilter('');
      setSelectedFlowId(normalizedFlows[0]?.id ?? null);
      setIsLoadDialogOpen(true);
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel listar os fluxos.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsLoadingFlowList(false);
    }
  };

  const handleOpenMarketplace = async () => {
    try {
      setUserMessage(null);
      setIsLoadingFlowList(true);
      const flows = await sendCommand<FlowSummary[]>('flow', 'listFlows', {});
      const catalog = await sendCommand<RecipeCatalogEntry[]>('flow', 'listRecipeCatalog', {});
      const normalizedFlows = Array.isArray(flows) ? flows : [];
      setRecipeCatalog(Array.isArray(catalog) ? catalog : []);
      setAvailableFlows(normalizedFlows);
      setMarketplaceFilter('');
      const firstRecipe = normalizedFlows.find((flow) => {
        const haystack = normalizeSearchText(`${flow.id} ${flow.name ?? ''}`);
        return haystack.includes('recipe') || haystack.includes('trae') || haystack.includes('portfolio') || Boolean(flow.isNative);
      });
      setSelectedMarketplaceFlowId(firstRecipe?.id ?? null);
      setIsMarketplaceOpen(true);
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel abrir o Marketplace.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsLoadingFlowList(false);
    }
  };

  const handleSelectFlow = async (targetFlowId = selectedFlowId) => {
    if (!targetFlowId) {
      setUserMessage({ type: 'info', text: 'Selecione um fluxo para carregar.' });
      return;
    }

    if (isDirty && !confirmDiscardUnsavedChanges('carregar outro fluxo')) {
      return;
    }

    try {
      setIsApplyingLoadedFlow(true);
      setUserMessage(null);
      clearNodeStatuses();
      await loadFlow(targetFlowId);
      const selectedFlow = availableFlows.find((flow) => flow.id === targetFlowId);
      const loadedFlowName = selectedFlow?.name?.trim() || 'Fluxo';
      setIsLoadDialogOpen(false);
      setIsMarketplaceOpen(false);
      setUserMessage({ type: 'success', text: `Fluxo "${loadedFlowName}" carregado com sucesso.` });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Fluxo "${loadedFlowName}" carregado com sucesso.`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel carregar o fluxo.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsApplyingLoadedFlow(false);
    }
  };

  const handleDeleteFlow = async (flow: FlowSummary) => {
    if (flow.isNative) {
      setUserMessage({ type: 'info', text: 'Fluxos nativos da ferramenta nao podem ser apagados.' });
      return;
    }

    const label = flow.name?.trim() || flow.id;
    const shouldDelete = window.confirm(`Deseja apagar a automacao "${label}"? Esta acao nao pode ser desfeita.`);
    if (!shouldDelete) {
      return;
    }

    try {
      setDeletingFlowId(flow.id);
      await sendCommand('flow', 'deleteFlow', { flowId: flow.id });
      const nextFlows = availableFlows.filter((candidate) => candidate.id !== flow.id);
      setAvailableFlows(nextFlows);
      if (selectedFlowId === flow.id) {
        setSelectedFlowId(nextFlows[0]?.id ?? null);
      }
      setUserMessage({ type: 'success', text: `Automacao "${label}" apagada com sucesso.` });
      addLog({
        timestamp: new Date().toISOString(),
        level: 'info',
        message: `Automacao "${label}" apagada com sucesso.`,
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel apagar a automacao selecionada.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setDeletingFlowId(null);
    }
  };

  const startMiraCapture = () => {
    setInspectorMode('mira');
    setUserMessage({ type: 'info', text: 'Mira ativo: clique no elemento que deseja capturar ou pressione Esc para cancelar.' });
    void sendCommand('platform', 'startMira', {}).catch((error) => {
      const message = getErrorMessage(error, 'Nao foi possivel iniciar o Mira.');
      setInspectorMode('none');
      setUserMessage({ type: 'error', text: message });
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
    });
  };

  const toggleMira = () => {
    if (inspectorMode === 'mira') {
      void sendCommand('platform', 'cancelInspector', {});
      setInspectorMode('none');
      setUserMessage({ type: 'info', text: 'Mira cancelado.' });
      return;
    }

    startMiraCapture();
  };

  const toggleSnip = () => {
    if (inspectorMode === 'snip') {
      void sendCommand('platform', 'cancelInspector', {});
      setInspectorMode('none');
      setUserMessage({ type: 'info', text: 'Snip cancelado.' });
    } else {
      setInspectorMode('snip');
      setUserMessage({ type: 'info', text: 'Snip ativo: arraste para selecionar uma regiao ou pressione Esc para cancelar.' });
      void sendCommand('platform', 'startSnip', {}).catch((error) => {
        const message = getErrorMessage(error, 'Nao foi possivel iniciar o Snip.');
        setInspectorMode('none');
        setUserMessage({ type: 'error', text: message });
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      });
    }
  };

  const toggleMacroRecorder = async () => {
    try {
      setIsMacroRecorderBusy(true);
      if (!macroRecorderActive) {
        const session = await sendCommand<MacroRecordingSession & { started?: boolean }>('platform', 'startMacroRecorder', {
          goal: 'Rascunho guiado',
          captureMouse: true,
          captureKeyboard: true,
          captureText: true,
          captureSensitiveText: false,
          stopHotkey: 'Ctrl+Shift+F12',
          maxEvents: 1000,
          idlePauseMs: 1500,
        });
        setMacroRecorderActive(true);
        setMacroRecorderStatus(session);
        setUserMessage({ type: 'info', text: 'Recorder global ativo. Grave a tarefa e pare para revisar a timeline.' });
        return;
      }

      const draft = await sendCommand<GuidedAutomationDraft>('platform', 'stopMacroRecorder', {});
      setMacroRecorderActive(false);
      setMacroRecorderStatus({
        sessionId: draft.sessionId ?? '',
        startedAt: draft.startedAt ?? new Date().toISOString(),
        stoppedAt: draft.stoppedAt ?? new Date().toISOString(),
        status: 'stopped',
        eventCount: draft.events.length,
        privacyMode: draft.events.some((event) => event.privacy?.isRedacted) ? 'redactSensitive' : 'default',
      });
      setGuidedDraft(draft);
      setUserMessage({ type: 'success', text: 'Gravacao pronta para revisao. Nada foi aplicado ou executado automaticamente.' });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel controlar o recorder.');
      setMacroRecorderActive(false);
      setMacroRecorderStatus(null);
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsMacroRecorderBusy(false);
    }
  };

  const openInspectionLibrary = () => {
    setInspectionFilter('');
    setSelectedInspectionAssetId(inspectionAssets[0]?.id ?? null);
    setInspectionAssetTestResult('');
    setIsInspectionLibraryOpen(true);
  };

  const captureFromInspectionLibrary = () => {
    setIsInspectionLibraryOpen(false);
    startMiraCapture();
  };

  const exportRunnerPackage = async () => {
    setIsExportingRunner(true);
    try {
      const backendFlow = toBackendFlow(flowId, flowName, [...nodes, ...stickyNotes], edges, {
        persistUiMetadata: true,
        variables: flowVariables,
      });
      const security = await sendCommand<SecurityReport>('engine', 'securityLint', backendFlow);
      let securityAcknowledged = false;
      if (!security.isSafeToRun) {
        if (!allowHighRiskExecution) {
          const message = buildSecurityBlockMessage(security);
          setUserMessage({ type: 'error', text: message });
          addLog({ timestamp: new Date().toISOString(), level: 'error', message });
          return;
        }

        const confirmed = await waitForHighRiskConfirmation(security);
        if (!confirmed) {
          setUserMessage({ type: 'info', text: 'Export cancelado pelo utilizador.' });
          return;
        }

        securityAcknowledged = true;
      }

      const exportPayload = withSecurityAckIfNeeded(backendFlow, security, securityAcknowledged);
      const result = await sendCommand<{ packageDirectory?: string }>('flow', 'exportRunnerPackage', exportPayload);
      const message = result?.packageDirectory
        ? `Pacote runner exportado em ${result.packageDirectory}`
        : 'Pacote runner exportado.';
      setUserMessage({ type: 'success', text: message });
      addLog({ timestamp: new Date().toISOString(), level: 'info', message });
    } catch (error) {
      const message = getErrorMessage(error, 'Falha ao exportar runner.');
      setUserMessage({ type: 'error', text: message });
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
    } finally {
      setIsExportingRunner(false);
    }
  };

  const latestCapturedInspection: CapturedElement | null = capturedElement;

  const refreshInspectionAssets = async () => {
    const assets = await sendCommand<InspectionAsset[]>('assets', 'listInspectionAssets', {});
    if (Array.isArray(assets)) {
      useAppStore.getState().setInspectionAssets(assets);
    }
  };

  const testSelectedInspectionAsset = async () => {
    if (!selectedInspectionAsset) {
      return;
    }

    setInspectionAssetBusyId(selectedInspectionAsset.id);
    setInspectionAssetTestResult('Testando seletor...');
    try {
      const result = await sendCommand<InspectionAssetTestResult>('assets', 'testInspectionAsset', {
        assetId: selectedInspectionAsset.id,
      });
      setInspectionAssetTestResult(
        result?.found
          ? `Seletor encontrado agora em ${formatBounds(result.bounds ?? selectedInspectionAsset.locator.absoluteBounds)}.`
          : 'Seletor nao encontrou o elemento agora; use fallback visual ou recapture.'
      );
    } catch (error) {
      setInspectionAssetTestResult(getErrorMessage(error, 'Falha ao testar seletor.'));
    } finally {
      setInspectionAssetBusyId(null);
    }
  };

  const deleteSelectedInspectionAsset = async () => {
    if (!selectedInspectionAsset) {
      return;
    }

    setInspectionAssetBusyId(selectedInspectionAsset.id);
    try {
      await sendCommand('assets', 'deleteInspectionAsset', { assetId: selectedInspectionAsset.id });
      await refreshInspectionAssets();
      setSelectedInspectionAssetId(null);
      setInspectionAssetTestResult('Ativo Mira apagado.');
    } catch (error) {
      setInspectionAssetTestResult(getErrorMessage(error, 'Falha ao apagar ativo Mira.'));
    } finally {
      setInspectionAssetBusyId(null);
    }
  };

  const saveSelectedInspectionAssetMetadata = async () => {
    if (!selectedInspectionAsset) {
      return;
    }

    setInspectionAssetBusyId(selectedInspectionAsset.id);
    try {
      const asset = await sendCommand<InspectionAsset>('assets', 'updateInspectionAsset', {
        assetId: selectedInspectionAsset.id,
        displayName: inspectionAssetNameDraft,
        notes: inspectionAssetNotesDraft,
        tags: inspectionAssetTagsDraft
          .split(',')
          .map((tag) => tag.trim())
          .filter(Boolean),
      });
      if (asset) {
        useAppStore.getState().upsertInspectionAsset(asset);
      }
      setInspectionAssetTestResult('Ativo Mira atualizado.');
    } catch (error) {
      setInspectionAssetTestResult(getErrorMessage(error, 'Falha ao salvar ativo Mira.'));
    } finally {
      setInspectionAssetBusyId(null);
    }
  };

  const duplicateSelectedInspectionAsset = async () => {
    if (!selectedInspectionAsset) {
      return;
    }

    setInspectionAssetBusyId(selectedInspectionAsset.id);
    try {
      const asset = await sendCommand<InspectionAsset>('assets', 'duplicateInspectionAsset', {
        assetId: selectedInspectionAsset.id,
        displayName: `${selectedInspectionAsset.displayName || 'Elemento salvo'} copia`,
      });
      if (asset) {
        useAppStore.getState().upsertInspectionAsset(asset);
        setSelectedInspectionAssetId(asset.id);
      }
      setInspectionAssetTestResult('Ativo Mira duplicado.');
    } catch (error) {
      setInspectionAssetTestResult(getErrorMessage(error, 'Falha ao duplicar ativo Mira.'));
    } finally {
      setInspectionAssetBusyId(null);
    }
  };

  const handleAddSticky = () => {
    const offsetX = 80 + Math.random() * 80;
    const offsetY = 80 + Math.random() * 80;
    addStickyNote({ x: offsetX, y: offsetY });
  };

  return (
    <>
      <div className="toolbar">
        <div className="toolbar__group">
          <button className="toolbar__btn" onClick={handleNew} title="Novo flow">
            <span className="toolbar__btn-icon">&#x1F4C4;</span>
            <span className="toolbar__btn-label">Novo</span>
          </button>
          <button className="toolbar__btn" onClick={handleSave} title="Salvar flow">
            <span className="toolbar__btn-icon">&#x1F4BE;</span>
            <span className="toolbar__btn-label">Salvar</span>
          </button>
          <button
            className={`toolbar__btn ${isLoadingFlowList ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleLoad}
            title="Carregar flow"
            disabled={isLoadingFlowList}
          >
            <span className="toolbar__btn-icon">&#x1F4C2;</span>
            <span className="toolbar__btn-label">{isLoadingFlowList ? 'Carregando...' : 'Carregar'}</span>
          </button>
          <button
            className={`toolbar__btn ${isLoadingFlowList ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void handleOpenMarketplace(); }}
            title="Receitas oficiais locais"
            disabled={isLoadingFlowList}
            type="button"
          >
            <span className="toolbar__btn-icon">&#x1F6D2;</span>
            <span className="toolbar__btn-label">Receitas</span>
          </button>
        </div>

        <div className="toolbar__divider" />

        <div className="toolbar__center">
          {isEditing ? (
            <input
              ref={inputRef}
              className="toolbar__name-input"
              value={editName}
              onChange={(event) => setEditName(event.target.value)}
              onBlur={commitName}
              onKeyDown={handleNameKeyDown}
            />
          ) : (
            <button
              className="toolbar__name"
              onClick={() => {
                setEditName(flowName);
                setIsEditing(true);
              }}
              title="Clique para renomear"
            >
              {flowName}
              {isDirty ? ' *' : ''}
              {isCurrentFlowArmed ? ' [Armed]' : ''}
            </button>
          )}
        </div>

        <div className="toolbar__divider" />

        <div className="toolbar__group">
          <button
            type="button"
            className={`toolbar__btn ${isAnalyzingHealth ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void analyzeFlowExperience(); }}
            title="Analisar saude do flow sem executar"
            disabled={isAnalyzingHealth}
          >
            <span className="toolbar__btn-icon">&#x2695;</span>
            <span className="toolbar__btn-label">
              {flowHealthReport ? `Saude ${flowHealthReport.score}` : 'Saude'}
            </span>
          </button>
          <button
            type="button"
            className={`toolbar__btn ${isRunningDryRun ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void runDryRun(); }}
            title="Dry-run: validar passos sem executar acoes"
            disabled={isRunningDryRun}
          >
            <span className="toolbar__btn-icon">&#x23ED;</span>
            <span className="toolbar__btn-label">{isRunningDryRun ? 'Testando...' : 'Dry-run'}</span>
          </button>
          <button
            className="toolbar__btn toolbar__btn--play"
            onClick={handlePlay}
            title="Executar este flow agora"
          >
            <span className="toolbar__btn-icon">&#9654;</span>
            <span className="toolbar__btn-label">Executar</span>
          </button>
          <button
            className={`toolbar__btn ${!canArmCurrentFlow || isCurrentFlowArmed ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleArm}
            disabled={!canArmCurrentFlow || isCurrentFlowArmed}
            title="Monitorar gatilhos deste flow"
          >
            <span className="toolbar__btn-icon">&#128276;</span>
            <span className="toolbar__btn-label">Monitorar</span>
          </button>
          <button
            className={`toolbar__btn ${!isCurrentFlowArmed ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleDisarm}
            disabled={!isCurrentFlowArmed}
            title="Desativar monitoramento deste flow"
          >
            <span className="toolbar__btn-icon">&#128277;</span>
            <span className="toolbar__btn-label">Desarmar</span>
          </button>
          <button
            className={`toolbar__btn toolbar__btn--stop ${!canStop ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void handleStop(); }}
            disabled={!canStop}
            title="Parar execucao atual"
          >
            <span className="toolbar__btn-icon">&#9632;</span>
            <span className="toolbar__btn-label">Parar</span>
          </button>
        </div>

        <div className="toolbar__divider" />

        <div className="toolbar__group">
          <button
            className={`toolbar__btn ${macroRecorderActive ? 'toolbar__btn--active' : ''} ${isMacroRecorderBusy ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void toggleMacroRecorder(); }}
            disabled={isMacroRecorderBusy}
            title={macroRecorderActive ? 'Parar recorder e revisar timeline' : 'Gravar passos em rascunho seguro'}
            type="button"
          >
            <span className="toolbar__btn-icon">&#x23FA;</span>
            <span className="toolbar__btn-label">{macroRecorderActive ? 'Parar rec' : 'Recorder'}</span>
          </button>
          <button
            className={`toolbar__btn ${inspectorMode === 'mira' ? 'toolbar__btn--active' : ''}`}
            onClick={toggleMira}
            title="Mira - capturar elemento da tela"
          >
            <span className="toolbar__btn-icon">&#x1F441;</span>
            <span className="toolbar__btn-label">Mira</span>
          </button>
          <button
            className={`toolbar__btn ${inspectorMode === 'snip' ? 'toolbar__btn--active' : ''}`}
            onClick={toggleSnip}
            title="Snip - capturar regiao da tela"
          >
            <span className="toolbar__btn-icon">&#x2702;</span>
            <span className="toolbar__btn-label">Snip</span>
          </button>
        </div>

        <div className="toolbar__divider" />

        <div className="toolbar__group">
          <details className="toolbar__more">
            <summary className="toolbar__more-summary" title="Comandos avancados">
              <span className="toolbar__btn-icon">&#8942;</span>
              <span className="toolbar__btn-label">Avancado</span>
            </summary>
            <div className="toolbar__more-panel">
              <button
                type="button"
                className={`toolbar__btn ${allowHighRiskExecution ? 'toolbar__btn--active' : ''}`}
                onClick={() => { void handleToggleHighRiskPermission(); }}
                title="Quando ativo, permite abrir o modal de confirmacao para executar, armar ou exportar fluxos com bloqueio de seguranca (exige securityAck)."
                aria-pressed={allowHighRiskExecution}
              >
                <span className="toolbar__btn-icon">&#9888;</span>
                <span className="toolbar__btn-label">{allowHighRiskExecution ? 'Risco: on' : 'Risco: off'}</span>
              </button>
              <button
                className={`toolbar__btn ${!canRestartCurrentFlow || isRestartingFlow ? 'toolbar__btn--disabled' : ''}`}
                onClick={() => { void handleRestart(); }}
                disabled={!canRestartCurrentFlow || isRestartingFlow}
                title="Cancela a execucao atual deste flow, remove pendencias dele e enfileira uma nova execucao"
                type="button"
              >
                <span className="toolbar__btn-icon">&#8635;</span>
                <span className="toolbar__btn-label">{isRestartingFlow ? 'Reiniciando...' : 'Reiniciar'}</span>
              </button>
              <button
                className={`toolbar__btn toolbar__btn--stop ${!canClearQueue || isClearingQueue ? 'toolbar__btn--disabled' : ''}`}
                onClick={() => { void handleClearQueue(); }}
                disabled={!canClearQueue || isClearingQueue}
                title="Remove apenas itens pendentes da fila, sem cancelar a execucao atual"
                type="button"
              >
                <span className="toolbar__btn-icon">&#8999;</span>
                <span className="toolbar__btn-label">{isClearingQueue ? 'Limpando...' : 'Limpar fila'}</span>
              </button>
              <button
                className={`toolbar__btn toolbar__btn--stop ${!canKillSwitch || isKillSwitching ? 'toolbar__btn--disabled' : ''}`}
                onClick={() => { void handleKillSwitch(); }}
                disabled={!canKillSwitch || isKillSwitching}
                title="Parar tudo: cancela runtime, limpa fila e desarma monitoramentos"
                type="button"
              >
                <span className="toolbar__btn-icon">&#x26D4;</span>
                <span className="toolbar__btn-label">{isKillSwitching ? 'Parando...' : 'Parar Tudo'}</span>
              </button>
              <button
                className={`toolbar__btn ${isExportingRunner ? 'toolbar__btn--disabled' : ''}`}
                onClick={() => { void exportRunnerPackage(); }}
                title="Exportar pacote runner executavel"
                disabled={isExportingRunner}
                type="button"
              >
                <span className="toolbar__btn-icon">&#x1F4E6;</span>
                <span className="toolbar__btn-label">{isExportingRunner ? 'Exportando...' : 'Exportar Runner'}</span>
              </button>
              <button
                className="toolbar__btn"
                onClick={openInspectionLibrary}
                title="Abrir biblioteca de capturas Mira"
                type="button"
              >
                <span className="toolbar__btn-icon">&#x1F5C2;</span>
                <span className="toolbar__btn-label">Mira Lib ({inspectionAssets.length})</span>
              </button>
              <button
                className="toolbar__btn"
                onClick={handleAddSticky}
                title="Adicionar nota ao canvas"
                aria-label="Adicionar nota adesiva"
              >
                <span className="toolbar__btn-icon">&#x1F4DD;</span>
                <span className="toolbar__btn-label">Nota</span>
              </button>
            </div>
          </details>
          <button
            className={`toolbar__btn ${debugVisualEnabled ? 'toolbar__btn--active' : ''}`}
            onClick={() => setDebugVisualEnabled(!debugVisualEnabled)}
            title="Ativar visual de debug (pulso em nodes em execução)"
            type="button"
          >
            <span className="toolbar__btn-icon">&#x1F41E;</span>
            <span className="toolbar__btn-label">{debugVisualEnabled ? 'Debug on' : 'Debug off'}</span>
          </button>
          <label
            className="toolbar__btn toolbar__locale"
            title={t('toolbar.languageSwitch')}
            aria-label={t('toolbar.languageSwitch')}
          >
            <span className="toolbar__btn-icon">&#x1F310;</span>
            <select
              className="toolbar__locale-select"
              value={locale}
              onChange={(event) => setLocale(event.target.value === 'en' ? 'en' : 'pt-BR')}
            >
              <option value="pt-BR">PT-BR</option>
              <option value="en">English</option>
            </select>
          </label>
        </div>
      </div>

      {isLoadDialogOpen && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => {
            if (!isApplyingLoadedFlow) {
              setIsLoadDialogOpen(false);
            }
          }}
        >
          <div
            className="toolbar__dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="load-flow-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="load-flow-dialog-title" className="toolbar__dialog-title">Carregar fluxo</h2>
                <p className="toolbar__dialog-subtitle">Escolha um fluxo salvo para abrir no editor.</p>
              </div>
              <button
                className="toolbar__dialog-close"
                onClick={() => setIsLoadDialogOpen(false)}
                disabled={isApplyingLoadedFlow}
                title="Fechar"
              >
                x
              </button>
            </div>

            {availableFlows.length > 0 && (
              <div className="toolbar__dialog-search">
                <input
                  ref={loadSearchInputRef}
                  className="toolbar__dialog-search-input"
                  type="text"
                  value={loadFilter}
                  onChange={(event) => setLoadFilter(event.target.value)}
                  placeholder="Buscar por nome ou id"
                  aria-label="Buscar fluxos"
                />
                <span className="toolbar__dialog-search-count">
                  {filteredFlows.length} de {availableFlows.length}
                </span>
              </div>
            )}

            {availableFlows.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhum fluxo salvo foi encontrado.
              </div>
            ) : filteredFlows.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhum fluxo corresponde ao filtro informado.
              </div>
            ) : (
              <div className="toolbar__dialog-list" role="listbox" aria-label="Fluxos salvos">
                {filteredFlows.map((flow) => (
                  <div key={flow.id} className="toolbar__flow-option-row">
                    <button
                      className={`toolbar__flow-option ${selectedFlowId === flow.id ? 'toolbar__flow-option--selected' : ''}`}
                      onClick={() => setSelectedFlowId(flow.id)}
                      onDoubleClick={() => { void handleSelectFlow(flow.id); }}
                      type="button"
                    >
                      <span className="toolbar__flow-option-header">
                        <span className="toolbar__flow-option-title">{flow.name?.trim() || 'Untitled Flow'}</span>
                        <span className="toolbar__flow-option-badges">
                          {flow.isNative && <span className="toolbar__flow-option-badge">Native</span>}
                          {isDemoFlow(flow) && <span className="toolbar__flow-option-badge">Demo</span>}
                        </span>
                      </span>
                      <span className="toolbar__flow-option-meta">
                        {flow.nodeCount ?? 0} no(s) • {formatModifiedAt(flow.modifiedAt)}
                      </span>
                      <span className="toolbar__flow-option-id">{flow.id}</span>
                    </button>
                    {!flow.isNative && (
                      <button
                        type="button"
                        className="toolbar__flow-delete-btn"
                        onClick={() => { void handleDeleteFlow(flow); }}
                        disabled={deletingFlowId === flow.id}
                        title="Apagar automacao nao nativa"
                      >
                        {deletingFlowId === flow.id ? '...' : '🗑'}
                      </button>
                    )}
                  </div>
                ))}
              </div>
            )}

            <div className="toolbar__dialog-actions">
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => setIsLoadDialogOpen(false)}
                disabled={isApplyingLoadedFlow}
                type="button"
              >
                Cancelar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--primary"
                onClick={() => { void handleSelectFlow(); }}
                disabled={!selectedFlowId || isApplyingLoadedFlow || filteredFlows.length === 0}
                type="button"
              >
                {isApplyingLoadedFlow ? 'Carregando...' : 'Carregar'}
              </button>
            </div>
          </div>
        </div>
      )}

      {isMarketplaceOpen && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => {
            if (!isApplyingLoadedFlow) {
              setIsMarketplaceOpen(false);
            }
          }}
        >
          <div
            className="toolbar__dialog toolbar__dialog--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="marketplace-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="marketplace-dialog-title" className="toolbar__dialog-title">Receitas</h2>
                <p className="toolbar__dialog-subtitle">
                  Receitas locais oficiais. Remoto fica bloqueado ate existir validacao, hash/assinatura e avisos de seguranca.
                </p>
              </div>
              <button
                className="toolbar__dialog-close"
                onClick={() => setIsMarketplaceOpen(false)}
                disabled={isApplyingLoadedFlow}
                title="Fechar"
              >
                x
              </button>
            </div>

            <div className="toolbar__asset-summary">
              <div className="toolbar__asset-summary-title">Receitas prontas para importar</div>
              <div className="toolbar__asset-summary-copy">
                Importe uma copia desarmada, revise os nodes, rode Saude/Dry-run e so depois execute ou monitore.
              </div>
              <div className="toolbar__asset-summary-copy toolbar__asset-summary-copy--warning">
                Downloads da internet nao executam automaticamente nesta versao.
              </div>
            </div>

            <div className="toolbar__dialog-search">
              <input
                ref={marketplaceSearchInputRef}
                className="toolbar__dialog-search-input"
                type="text"
                value={marketplaceFilter}
                onChange={(event) => setMarketplaceFilter(event.target.value)}
                placeholder="Buscar receita, Trae, overlay, console..."
                aria-label="Buscar receitas"
              />
              <span className="toolbar__dialog-search-count">
                {marketplaceFlows.length} receita(s)
              </span>
            </div>

            {marketplaceFlows.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhuma receita local encontrada. Reinicie o Sidekick para semear os flows oficiais se esta lista estiver vazia.
              </div>
            ) : (
              <div className="toolbar__dialog-list" role="listbox" aria-label="Receitas locais">
                {marketplaceFlows.map((flow) => {
                  const recipeInfo = recipeCatalog.find((entry) => entry.id === flow.id);
                  return (
                    <button
                      key={flow.id}
                      className={`toolbar__flow-option ${selectedMarketplaceFlowId === flow.id ? 'toolbar__flow-option--selected' : ''}`}
                      onClick={() => setSelectedMarketplaceFlowId(flow.id)}
                      onDoubleClick={() => { void handleSelectFlow(flow.id); }}
                      type="button"
                    >
                      <span className="toolbar__flow-option-header">
                        {recipeInfo && isElevatedCatalogRisk(recipeInfo.risk) ? (
                          <span
                            className="toolbar__flow-option-alert"
                            title={`Receita com risco catalogado: ${recipeInfo.risk}. Revise os nodes antes de executar.`}
                            aria-hidden
                          >
                            &#9888;
                          </span>
                        ) : null}
                        <span className="toolbar__flow-option-title">{flow.name?.trim() || flow.id}</span>
                        <span className="toolbar__flow-option-badges">
                          <span className="toolbar__flow-option-badge">Recipe</span>
                          {recipeInfo && <span className="toolbar__flow-option-badge">Popular {recipeInfo.popularity}</span>}
                          {recipeInfo?.risk && <span className="toolbar__flow-option-badge">{recipeInfo.risk}</span>}
                          {flow.isNative && <span className="toolbar__flow-option-badge">Native</span>}
                          {flow.preflightStatus && (
                            <span
                              className={`toolbar__flow-option-badge toolbar__flow-option-badge--${flow.preflightStatus}`}
                              title={flow.preflightMessage ?? getPreflightLabel(flow)}
                            >
                              {getPreflightLabel(flow)}
                            </span>
                          )}
                        </span>
                      </span>
                      <span className="toolbar__flow-option-meta">
                        {flow.nodeCount ?? 0} no(s) • {formatModifiedAt(flow.modifiedAt)}
                      </span>
                      {recipeInfo && (
                        <span className="toolbar__flow-option-meta">
                          {recipeInfo.category} • {recipeInfo.persona}
                        </span>
                      )}
                      <span className="toolbar__flow-option-id">{flow.id}</span>
                    </button>
                  );
                })}
              </div>
            )}

            <div className="toolbar__dialog-actions">
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => setIsMarketplaceOpen(false)}
                disabled={isApplyingLoadedFlow}
                type="button"
              >
                Cancelar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--primary"
                onClick={() => { void handleSelectFlow(selectedMarketplaceFlowId); }}
                disabled={!selectedMarketplaceFlowId || isApplyingLoadedFlow || marketplaceFlows.length === 0}
                type="button"
              >
                {isApplyingLoadedFlow ? 'Importando...' : 'Importar copia desarmada'}
              </button>
            </div>
          </div>
        </div>
      )}

      {highRiskDialogSecurity && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => resolveHighRiskDialog(false)}
          role="presentation"
        >
          <div
            className="toolbar__dialog toolbar__dialog--compact"
            role="dialog"
            aria-modal="true"
            aria-labelledby="high-risk-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="high-risk-dialog-title" className="toolbar__dialog-title">
                  Confirmar fluxo de alto risco
                </h2>
                <p className="toolbar__dialog-subtitle">
                  Nivel: {highRiskDialogSecurity.riskLevel}. Hash de manifesto: {(highRiskDialogSecurity.manifestHash ?? '').slice(0, 12)}...
                </p>
              </div>
            </div>
            <div className="toolbar__asset-summary">
              <div className="toolbar__asset-summary-title">Alertas de seguranca</div>
              <ul className="toolbar__high-risk-issue-list">
                {highRiskDialogSecurity.issues.map((issue) => (
                  <li key={`${issue.code}-${issue.nodeId ?? ''}-${issue.message}`}>
                    <strong>{issue.severity}</strong>: {issue.message}
                  </li>
                ))}
              </ul>
            </div>
            <div className="toolbar__dialog-actions">
              <button
                type="button"
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => resolveHighRiskDialog(false)}
              >
                Cancelar
              </button>
              <button
                type="button"
                className="toolbar__dialog-btn toolbar__dialog-btn--primary"
                onClick={() => resolveHighRiskDialog(true)}
              >
                Confirmar e continuar
              </button>
            </div>
          </div>
        </div>
      )}

      {isDryRunDialogOpen && dryRunReport && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => setIsDryRunDialogOpen(false)}
        >
          <div
            className="toolbar__dialog toolbar__dialog--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="dry-run-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="dry-run-dialog-title" className="toolbar__dialog-title">Dry-run</h2>
                <p className="toolbar__dialog-subtitle">
                  Validacao local sem executar acoes. Flow Health {dryRunReport.health.score}/100 ({dryRunReport.health.level}).
                </p>
              </div>
              <button
                className="toolbar__dialog-close"
                onClick={() => setIsDryRunDialogOpen(false)}
                title="Fechar"
                type="button"
              >
                x
              </button>
            </div>

            <div className="toolbar__asset-summary">
              <div className="toolbar__asset-summary-title">{dryRunReport.summary}</div>
              <div className="toolbar__asset-summary-copy">
                {dryRunReport.canRun
                  ? 'Nenhuma acao foi disparada. Use Executar apenas quando os passos fizerem sentido para o estado atual da janela.'
                  : 'Revise os bloqueios antes de executar ou armar este flow.'}
              </div>
            </div>

            <div className="toolbar__dryrun-layout">
              <div className="toolbar__dryrun-list" role="list" aria-label="Passos do dry-run">
                {dryRunReport.steps.length === 0 ? (
                  <div className="toolbar__dialog-empty">Nenhum passo planejado.</div>
                ) : dryRunReport.steps.map((step, index) => (
                  <div key={`${step.nodeId}-${index}`} className={`toolbar__dryrun-step toolbar__dryrun-step--${String(step.status).toLowerCase()}`}>
                    <div className="toolbar__dryrun-step-index">{index + 1}</div>
                    <div className="toolbar__dryrun-step-copy">
                      <strong>{step.displayName || step.typeId}</strong>
                      <span>{step.message || step.typeId}</span>
                      {step.requiresConfirmation && (
                        <em>Pausa de confirmacao antes deste passo</em>
                      )}
                    </div>
                    <span className="toolbar__dryrun-step-status">{step.status}</span>
                  </div>
                ))}
              </div>

              <div className="toolbar__dryrun-side">
                <div className="toolbar__asset-detail-card">
                  <span className="toolbar__asset-detail-label">Problemas</span>
                  <strong>{dryRunReport.health.issues.length}</strong>
                  <span className="toolbar__asset-detail-copy">
                    {dryRunReport.health.issues[0]?.message ?? 'Nenhum problema critico encontrado.'}
                  </span>
                </div>
                <div className="toolbar__asset-detail-card">
                  <span className="toolbar__asset-detail-label">Sugestoes</span>
                  <strong>{dryRunReport.health.suggestions.length}</strong>
                  <span className="toolbar__asset-detail-copy">
                    {dryRunReport.health.suggestions[0]?.title ?? 'Sem sugestoes adicionais.'}
                  </span>
                </div>
                <div className="toolbar__asset-detail-card">
                  <span className="toolbar__asset-detail-label">Checkpoints</span>
                  <strong>{dryRunReport.checkpoints.length}</strong>
                  <span className="toolbar__asset-detail-copy">
                    {dryRunReport.checkpoints[0]?.message ?? 'Sem pausa obrigatoria.'}
                  </span>
                </div>
              </div>
            </div>

            <div className="toolbar__dialog-actions">
              <button
                type="button"
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => setIsDryRunDialogOpen(false)}
              >
                Fechar
              </button>
              <button
                type="button"
                className="toolbar__dialog-btn toolbar__dialog-btn--primary"
                onClick={() => { void runDryRun(); }}
              >
                Repetir dry-run
              </button>
            </div>
          </div>
        </div>
      )}

      {isStopDialogOpen && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => {
            if (!isStoppingFlow) {
              setIsStopDialogOpen(false);
            }
          }}
        >
          <div
            className="toolbar__dialog toolbar__dialog--compact"
            role="dialog"
            aria-modal="true"
            aria-labelledby="stop-runtime-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="stop-runtime-dialog-title" className="toolbar__dialog-title">Parar runtime</h2>
                <p className="toolbar__dialog-subtitle">
                  {currentRun
                    ? `Existe uma execucao em andamento para "${currentRun.flowName}".`
                    : 'Existem itens aguardando na fila do runtime.'}
                </p>
              </div>
              <button
                className="toolbar__dialog-close"
                onClick={() => setIsStopDialogOpen(false)}
                disabled={isStoppingFlow}
                title="Fechar"
              >
                x
              </button>
            </div>

            <div className="toolbar__dialog-body">
              <p className="toolbar__dialog-copy">
                Fila atual: {queueLength} item(ns).
              </p>
              <p className="toolbar__dialog-copy">
                Escolha se deseja apenas interromper a execucao atual ou tambem limpar toda a fila pendente.
              </p>
            </div>

            <div className="toolbar__dialog-actions">
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => setIsStopDialogOpen(false)}
                disabled={isStoppingFlow}
                type="button"
              >
                Cancelar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => { void executeStop('currentOnly'); }}
                disabled={!isRunning || isStoppingFlow}
                type="button"
              >
                {isStoppingFlow ? 'Parando...' : 'Parar atual'}
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => { void handleClearQueue(); }}
                disabled={!canClearQueue || isClearingQueue || isStoppingFlow}
                type="button"
              >
                {isClearingQueue ? 'Limpando...' : 'Limpar fila'}
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--danger"
                onClick={() => { void executeStop('cancelAll'); }}
                disabled={isStoppingFlow}
                type="button"
              >
                {isStoppingFlow ? 'Limpando...' : 'Parar e limpar fila'}
              </button>
            </div>
          </div>
        </div>
      )}

      {isInspectionLibraryOpen && (
        <div
          className="toolbar__dialog-backdrop"
          onClick={() => setIsInspectionLibraryOpen(false)}
        >
          <div
            className="toolbar__dialog toolbar__dialog--wide"
            role="dialog"
            aria-modal="true"
            aria-labelledby="inspection-library-dialog-title"
            onClick={(event) => event.stopPropagation()}
          >
            <div className="toolbar__dialog-header">
              <div>
                <h2 id="inspection-library-dialog-title" className="toolbar__dialog-title">Biblioteca Mira</h2>
                <p className="toolbar__dialog-subtitle">
                  Navegue pelos elementos capturados e reutilize este historico como base para futuros bindings.
                </p>
              </div>
              <button
                className="toolbar__dialog-close"
                onClick={() => setIsInspectionLibraryOpen(false)}
                title="Fechar"
                type="button"
              >
                x
              </button>
            </div>

            {latestCapturedInspection && (
              <div className="toolbar__asset-summary">
                <strong className="toolbar__asset-summary-title">Ultima captura</strong>
                <span className="toolbar__asset-summary-copy">
                  {(latestCapturedInspection.asset?.displayName ?? latestCapturedInspection.name ?? latestCapturedInspection.automationId ?? 'Elemento capturado')}
                  {' • '}
                  {latestCapturedInspection.controlType || 'tipo desconhecido'}
                  {' • '}
                  {formatBounds(latestCapturedInspection.boundingRect)}
                </span>
                {latestCapturedInspection.assetSaveError && (
                  <span className="toolbar__asset-summary-copy toolbar__asset-summary-copy--warning">
                    Persistencia falhou: {latestCapturedInspection.assetSaveError}
                  </span>
                )}
              </div>
            )}

            {inspectionAssets.length > 0 && (
              <div className="toolbar__dialog-search">
                <input
                  ref={inspectionSearchInputRef}
                  className="toolbar__dialog-search-input"
                  type="text"
                  value={inspectionFilter}
                  onChange={(event) => setInspectionFilter(event.target.value)}
                  placeholder="Buscar por nome, seletor, janela ou tag"
                  aria-label="Buscar ativos Mira"
                />
                <span className="toolbar__dialog-search-count">
                  {filteredInspectionAssets.length} de {inspectionAssets.length}
                </span>
              </div>
            )}

            {inspectionAssets.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhum ativo Mira salvo ainda. Use o botao Mira para capturar um elemento.
              </div>
            ) : filteredInspectionAssets.length === 0 ? (
              <div className="toolbar__dialog-empty">
                Nenhum ativo Mira corresponde ao filtro informado.
              </div>
            ) : (
              <div className="toolbar__asset-layout">
                <div className="toolbar__dialog-list" role="listbox" aria-label="Ativos Mira salvos">
                  {filteredInspectionAssets.map((asset) => (
                    <button
                      key={asset.id}
                      className={`toolbar__flow-option ${selectedInspectionAsset?.id === asset.id ? 'toolbar__flow-option--selected' : ''}`}
                      onClick={() => setSelectedInspectionAssetId(asset.id)}
                      type="button"
                    >
                      {asset.content.thumbnailBase64 && (
                        <img
                          className="toolbar__asset-thumb"
                          src={`data:image/png;base64,${asset.content.thumbnailBase64}`}
                          alt=""
                        />
                      )}
                      <span className="toolbar__flow-option-header">
                        <span className="toolbar__flow-option-title">{asset.displayName?.trim() || 'Elemento salvo'}</span>
                        <span className="toolbar__flow-option-badge">Mira</span>
                      </span>
                      <span className="toolbar__flow-option-meta">
                        {buildInspectionSummary(asset)}
                      </span>
                      <span className="toolbar__flow-option-meta">
                        {asset.source.windowTitle || 'Janela indisponivel'} • {formatInspectionCapturedAt(asset.updatedAt)}
                      </span>
                      <span className="toolbar__flow-option-id">{asset.id}</span>
                    </button>
                  ))}
                </div>

                {selectedInspectionAsset && (
                  <div className="toolbar__asset-detail">
                    {selectedInspectionAsset.content.thumbnailBase64 && (
                      <img
                        className="toolbar__asset-preview"
                        src={`data:image/png;base64,${selectedInspectionAsset.content.thumbnailBase64}`}
                        alt=""
                      />
                    )}
                    <div className="toolbar__asset-detail-section">
                      <strong className="toolbar__asset-detail-title">{selectedInspectionAsset.displayName}</strong>
                      <p className="toolbar__asset-detail-copy">{buildInspectionSummary(selectedInspectionAsset)}</p>
                    </div>

                    <div className="toolbar__asset-detail-section">
                      <label className="toolbar__asset-detail-label" htmlFor="mira-asset-name">Nome</label>
                      <input
                        id="mira-asset-name"
                        className="toolbar__dialog-search"
                        value={inspectionAssetNameDraft}
                        onChange={(event) => setInspectionAssetNameDraft(event.target.value)}
                      />
                      <label className="toolbar__asset-detail-label" htmlFor="mira-asset-notes">Notas</label>
                      <textarea
                        id="mira-asset-notes"
                        className="toolbar__asset-notes"
                        value={inspectionAssetNotesDraft}
                        onChange={(event) => setInspectionAssetNotesDraft(event.target.value)}
                      />
                      <label className="toolbar__asset-detail-label" htmlFor="mira-asset-tags">Tags</label>
                      <input
                        id="mira-asset-tags"
                        className="toolbar__dialog-search"
                        value={inspectionAssetTagsDraft}
                        onChange={(event) => setInspectionAssetTagsDraft(event.target.value)}
                        placeholder="busca, campo-texto, revisar"
                      />
                    </div>

                    <div className="toolbar__asset-detail-section">
                      <strong className="toolbar__asset-detail-label">Strategia</strong>
                      <p className="toolbar__asset-detail-copy">
                        {selectedInspectionAsset.locator.strategy}
                        {' • '}
                        selector {selectedInspectionAsset.locator.strength ?? 'fraca'}
                      </p>
                      {selectedInspectionAsset.locator.strengthReason && (
                        <p className="toolbar__asset-detail-copy">{selectedInspectionAsset.locator.strengthReason}</p>
                      )}
                    </div>

                    <div className="toolbar__asset-detail-grid">
                      <div className="toolbar__asset-detail-card">
                        <strong className="toolbar__asset-detail-label">Texto</strong>
                        <p className="toolbar__asset-detail-copy">
                          {selectedInspectionAsset.content.detectedText || 'Nao detectado'}
                        </p>
                        <p className="toolbar__asset-detail-copy">
                          atual: {selectedInspectionAsset.content.currentText || '(vazio)'}
                        </p>
                        <p className="toolbar__asset-detail-copy">
                          hint: {selectedInspectionAsset.content.placeholderText || '(nenhum)'}
                        </p>
                        <p className="toolbar__asset-detail-copy">
                          origem {selectedInspectionAsset.content.textSource || 'fallback'} / {selectedInspectionAsset.content.captureQuality || selectedInspectionAsset.locator.strength || 'fraca'}
                        </p>
                        {selectedInspectionAsset.content.ocrWarning && (
                          <p className="toolbar__asset-detail-copy">{selectedInspectionAsset.content.ocrWarning}</p>
                        )}
                      </div>
                      <div className="toolbar__asset-detail-card">
                        <strong className="toolbar__asset-detail-label">Janela</strong>
                        <p className="toolbar__asset-detail-copy">{selectedInspectionAsset.source.windowTitle || 'Indisponivel'}</p>
                      </div>
                      <div className="toolbar__asset-detail-card">
                        <strong className="toolbar__asset-detail-label">Processo</strong>
                        <p className="toolbar__asset-detail-copy">
                          {selectedInspectionAsset.source.processName || 'Indisponivel'}
                          {selectedInspectionAsset.source.processId ? ` (#${selectedInspectionAsset.source.processId})` : ''}
                        </p>
                        {selectedInspectionAsset.source.processPath && (
                          <p className="toolbar__asset-detail-copy">{selectedInspectionAsset.source.processPath}</p>
                        )}
                      </div>
                      <div className="toolbar__asset-detail-card">
                        <strong className="toolbar__asset-detail-label">Selector</strong>
                        <p className="toolbar__asset-detail-copy">
                          {selectedInspectionAsset.locator.selector.automationId
                            || selectedInspectionAsset.locator.selector.name
                            || selectedInspectionAsset.locator.selector.className
                            || 'Indisponivel'}
                        </p>
                      </div>
                      <div className="toolbar__asset-detail-card">
                        <strong className="toolbar__asset-detail-label">Bounds</strong>
                        <p className="toolbar__asset-detail-copy">
                          {formatBounds(selectedInspectionAsset.locator.absoluteBounds)}
                        </p>
                        <p className="toolbar__asset-detail-copy">
                          Rel {formatBounds(selectedInspectionAsset.locator.relativeBounds)}
                        </p>
                      </div>
                    </div>

                    {inspectionAssetTestResult && (
                      <div className="toolbar__asset-detail-section">
                        <p className="toolbar__asset-detail-copy">{inspectionAssetTestResult}</p>
                      </div>
                    )}

                    {selectedInspectionAsset.tags.length > 0 && (
                      <div className="toolbar__asset-detail-section">
                        <strong className="toolbar__asset-detail-label">Tags</strong>
                        <div className="toolbar__asset-tags">
                          {selectedInspectionAsset.tags.map((tag) => (
                            <span key={tag} className="toolbar__asset-tag">{tag}</span>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}

            <div className="toolbar__dialog-actions">
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => setIsInspectionLibraryOpen(false)}
                type="button"
              >
                Fechar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => { void saveSelectedInspectionAssetMetadata(); }}
                disabled={!selectedInspectionAsset || inspectionAssetBusyId === selectedInspectionAsset?.id}
                type="button"
              >
                Salvar ativo
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => { void duplicateSelectedInspectionAsset(); }}
                disabled={!selectedInspectionAsset || inspectionAssetBusyId === selectedInspectionAsset?.id}
                type="button"
              >
                Duplicar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => { void testSelectedInspectionAsset(); }}
                disabled={!selectedInspectionAsset || inspectionAssetBusyId === selectedInspectionAsset?.id}
                type="button"
              >
                Test Selector
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--secondary"
                onClick={() => { void deleteSelectedInspectionAsset(); }}
                disabled={!selectedInspectionAsset || inspectionAssetBusyId === selectedInspectionAsset?.id}
                type="button"
              >
                Apagar
              </button>
              <button
                className="toolbar__dialog-btn toolbar__dialog-btn--primary"
                onClick={captureFromInspectionLibrary}
                type="button"
              >
                Capturar com Mira
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
