import { useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { sendCommand } from '../../bridge/bridge';
import { toBackendFlow } from '../../bridge/flowConverter';
import type {
  CapturedElement,
  FlowValidationResult,
  FlowNodeData,
  FlowRuntimeSnapshot,
  InspectionAsset,
  InspectionAssetTestResult,
  StopFlowMode,
} from '../../bridge/types';
import { useAppStore } from '../../store/appStore';
import { useFlowStore } from '../../store/flowStore';

interface FlowSummary {
  id: string;
  name?: string;
  modifiedAt?: string;
  nodeCount?: number;
  isNative?: boolean;
}

interface FlowActivationResponse {
  armed?: boolean;
  flow?: FlowRuntimeSnapshot;
  warnings?: string[];
  validation?: FlowValidationResult;
}

interface RunFlowResponse {
  queued?: boolean;
  flowId?: string;
  queueLength?: number;
  queuePending?: boolean;
  validation?: FlowValidationResult;
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
    asset.content.automationId,
    asset.locator.strength,
    asset.locator.strategy,
  ].filter(Boolean);

  return summaryParts.length > 0 ? summaryParts.join(' • ') : 'Metadados basicos indisponiveis';
}

function hasContinuousTriggers(nodes: Array<{ data: FlowNodeData }>): boolean {
  return nodes.some((node) => node.data.category === 'Trigger' && node.data.typeId !== 'trigger.manualStart');
}

function buildStopMessage(mode: StopFlowMode, queueLength: number): string {
  return mode === 'cancelAll'
    ? `Execucao atual interrompida e ${queueLength} item(ns) removido(s) da fila.`
    : 'Execucao atual interrompida.';
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

export default function Toolbar() {
  const flowName = useFlowStore((s) => s.flowName);
  const flowId = useFlowStore((s) => s.flowId);
  const isDirty = useFlowStore((s) => s.isDirty);
  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
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
  const [isApplyingLoadedFlow, setIsApplyingLoadedFlow] = useState(false);
  const [deletingFlowId, setDeletingFlowId] = useState<string | null>(null);
  const [isStopDialogOpen, setIsStopDialogOpen] = useState(false);
  const [isStoppingFlow, setIsStoppingFlow] = useState(false);
  const [isInspectionLibraryOpen, setIsInspectionLibraryOpen] = useState(false);
  const [inspectionFilter, setInspectionFilter] = useState('');
  const [selectedInspectionAssetId, setSelectedInspectionAssetId] = useState<string | null>(null);
  const [inspectionAssetBusyId, setInspectionAssetBusyId] = useState<string | null>(null);
  const [inspectionAssetTestResult, setInspectionAssetTestResult] = useState<string>('');
  const inputRef = useRef<HTMLInputElement>(null);
  const loadSearchInputRef = useRef<HTMLInputElement>(null);
  const marketplaceSearchInputRef = useRef<HTMLInputElement>(null);
  const inspectionSearchInputRef = useRef<HTMLInputElement>(null);

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
    return availableFlows
      .filter((flow) => {
        const haystack = normalizeSearchText(`${flow.id} ${flow.name ?? ''}`);
        const isRecipe = haystack.includes('recipe')
          || haystack.includes('trae')
          || haystack.includes('portfolio')
          || Boolean(flow.isNative);

        return isRecipe && (!normalizedFilter || haystack.includes(normalizedFilter));
      })
      .sort((left, right) => (left.name ?? left.id).localeCompare(right.name ?? right.id, 'pt-BR'));
  }, [availableFlows, marketplaceFilter]);

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
  const canArmCurrentFlow = hasContinuousTriggers(nodes);
  const canStop = isRunning || queueLength > 0;

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
      const validation = await validateFlow();
      if (!validation.isValid) {
        const message = buildValidationErrorMessage(validation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      clearNodeStatuses();
      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges);
      const result = await sendCommand<RunFlowResponse>('engine', 'runFlow', backendFlow);
      const runtimeValidation = result?.validation ?? validation;

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

  const handleArm = async () => {
    try {
      setUserMessage(null);
      const validation = await validateFlow();
      if (!validation.isValid) {
        const message = buildValidationErrorMessage(validation);
        addLog({ timestamp: new Date().toISOString(), level: 'error', message });
        setUserMessage({ type: 'error', text: message });
        return;
      }

      const backendFlow = toBackendFlow(flowId, flowName, nodes, edges);
      const result = await sendCommand<FlowActivationResponse>('engine', 'activateFlow', backendFlow);
      const runtimeValidation = result?.validation ?? validation;
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
      await sendCommand('engine', 'stopFlow', { mode });
      setIsStopDialogOpen(false);
      setUserMessage({
        type: 'info',
        text: buildStopMessage(mode, queueLength),
      });
    } catch (error) {
      const message = getErrorMessage(error, 'Nao foi possivel interromper o runtime.');
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
      setUserMessage({ type: 'error', text: message });
    } finally {
      setIsStoppingFlow(false);
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
      const normalizedFlows = Array.isArray(flows) ? flows : [];
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
      const normalizedFlows = Array.isArray(flows) ? flows : [];
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

  return (
    <>
      <div className="toolbar">
        <div className="toolbar__group">
          <button className="toolbar__btn" onClick={handleNew} title="New Flow">
            <span className="toolbar__btn-icon">&#x1F4C4;</span>
            <span className="toolbar__btn-label">New</span>
          </button>
          <button className="toolbar__btn" onClick={handleSave} title="Save Flow">
            <span className="toolbar__btn-icon">&#x1F4BE;</span>
            <span className="toolbar__btn-label">Save</span>
          </button>
          <button
            className={`toolbar__btn ${isLoadingFlowList ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleLoad}
            title="Load Flow"
            disabled={isLoadingFlowList}
          >
            <span className="toolbar__btn-icon">&#x1F4C2;</span>
            <span className="toolbar__btn-label">{isLoadingFlowList ? 'Loading...' : 'Load'}</span>
          </button>
          <button
            className={`toolbar__btn ${isLoadingFlowList ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void handleOpenMarketplace(); }}
            title="Marketplace de receitas prontas"
            disabled={isLoadingFlowList}
            type="button"
          >
            <span className="toolbar__btn-icon">&#x1F6D2;</span>
            <span className="toolbar__btn-label">Marketplace</span>
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
              title="Click to rename"
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
            className="toolbar__btn toolbar__btn--play"
            onClick={handlePlay}
            title="Queue an immediate run"
          >
            <span className="toolbar__btn-icon">&#9654;</span>
            <span className="toolbar__btn-label">Run Now</span>
          </button>
          <button
            className={`toolbar__btn ${!canArmCurrentFlow || isCurrentFlowArmed ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleArm}
            disabled={!canArmCurrentFlow || isCurrentFlowArmed}
            title="Arm flow triggers"
          >
            <span className="toolbar__btn-icon">&#128276;</span>
            <span className="toolbar__btn-label">Arm</span>
          </button>
          <button
            className={`toolbar__btn ${!isCurrentFlowArmed ? 'toolbar__btn--disabled' : ''}`}
            onClick={handleDisarm}
            disabled={!isCurrentFlowArmed}
            title="Disarm flow triggers"
          >
            <span className="toolbar__btn-icon">&#128277;</span>
            <span className="toolbar__btn-label">Disarm</span>
          </button>
          <button
            className={`toolbar__btn toolbar__btn--stop ${!canStop ? 'toolbar__btn--disabled' : ''}`}
            onClick={() => { void handleStop(); }}
            disabled={!canStop}
            title="Stop current run"
          >
            <span className="toolbar__btn-icon">&#9632;</span>
            <span className="toolbar__btn-label">Stop</span>
          </button>
        </div>

        <div className="toolbar__divider" />

        <div className="toolbar__group">
          <button
            className={`toolbar__btn ${inspectorMode === 'mira' ? 'toolbar__btn--active' : ''}`}
            onClick={toggleMira}
            title="Mira Inspector - Element detection"
          >
            <span className="toolbar__btn-icon">&#x1F441;</span>
            <span className="toolbar__btn-label">Mira</span>
          </button>
          <button
            className="toolbar__btn"
            onClick={openInspectionLibrary}
            title="Browse saved Mira inspection assets"
            type="button"
          >
            <span className="toolbar__btn-icon">&#x1F5C2;</span>
            <span className="toolbar__btn-label">Mira Lib ({inspectionAssets.length})</span>
          </button>
          <button
            className={`toolbar__btn ${inspectorMode === 'snip' ? 'toolbar__btn--active' : ''}`}
            onClick={toggleSnip}
            title="Snip - Screen region capture"
          >
            <span className="toolbar__btn-icon">&#x2702;</span>
            <span className="toolbar__btn-label">Snip</span>
          </button>
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
                <h2 id="marketplace-dialog-title" className="toolbar__dialog-title">Marketplace</h2>
                <p className="toolbar__dialog-subtitle">
                  Receitas locais oficiais. Marketplace remoto fica bloqueado ate existir validacao, hash/assinatura e avisos de seguranca.
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
                Abra uma receita como copia editavel, revise os nodes e rode manualmente antes de armar qualquer automacao.
              </div>
              <div className="toolbar__asset-summary-copy toolbar__asset-summary-copy--warning">
                Downloads da internet ainda nao sao executados automaticamente por seguranca.
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
                aria-label="Buscar marketplace"
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
              <div className="toolbar__dialog-list" role="listbox" aria-label="Receitas do marketplace">
                {marketplaceFlows.map((flow) => (
                  <button
                    key={flow.id}
                    className={`toolbar__flow-option ${selectedMarketplaceFlowId === flow.id ? 'toolbar__flow-option--selected' : ''}`}
                    onClick={() => setSelectedMarketplaceFlowId(flow.id)}
                    onDoubleClick={() => { void handleSelectFlow(flow.id); }}
                    type="button"
                  >
                    <span className="toolbar__flow-option-header">
                      <span className="toolbar__flow-option-title">{flow.name?.trim() || flow.id}</span>
                      <span className="toolbar__flow-option-badges">
                        <span className="toolbar__flow-option-badge">Recipe</span>
                        {flow.isNative && <span className="toolbar__flow-option-badge">Native</span>}
                      </span>
                    </span>
                    <span className="toolbar__flow-option-meta">
                      {flow.nodeCount ?? 0} no(s) • {formatModifiedAt(flow.modifiedAt)}
                    </span>
                    <span className="toolbar__flow-option-id">{flow.id}</span>
                  </button>
                ))}
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
                {isApplyingLoadedFlow ? 'Abrindo...' : 'Abrir receita'}
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
                    <div className="toolbar__asset-detail-section">
                      <strong className="toolbar__asset-detail-title">{selectedInspectionAsset.displayName}</strong>
                      <p className="toolbar__asset-detail-copy">{buildInspectionSummary(selectedInspectionAsset)}</p>
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
