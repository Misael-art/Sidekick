import { useCallback, useEffect, useRef, useMemo, useState, type DragEvent, type MouseEvent as ReactMouseEvent } from 'react';
import {
  ReactFlow,
  MiniMap,
  Controls,
  Background,
  BackgroundVariant,
  type ReactFlowInstance,
  type Node,
  type Edge,
  type Connection,
  type IsValidConnection,
  type OnNodesChange,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { sendCommand } from '../../bridge/bridge';
import type { FlowNodeData, GuidedAutomationDraft, MacroRecordingSession } from '../../bridge/types';
import type { StickyNoteData } from '../../bridge/flowConverter';
import TriggerNode from '../Nodes/TriggerNode';
import LogicNode from '../Nodes/LogicNode';
import ActionNode from '../Nodes/ActionNode';
import StickyNote from '../Nodes/StickyNote';
import { useFlowStore } from '../../store/flowStore';
import { useAppStore } from '../../store/appStore';
import { createMiraSelectorOverrides, createSnipTemplateOverrides } from '../../utils/captureNodePrefill';
import { getNodeProductCategory } from '../../utils/nodeProductCategory';

const nodeTypes = {
  triggerNode: TriggerNode,
  logicNode: LogicNode,
  actionNode: ActionNode,
  stickyNote: StickyNote,
};

type CanvasPointerEvent = ReactMouseEvent<Element> | globalThis.MouseEvent;
type CanvasPointerLikeEvent = CanvasPointerEvent | globalThis.TouchEvent;
const CONTEXT_MENU_WIDTH = 320;
const CONTEXT_MENU_MAX_HEIGHT = 620;
const NODE_COMMAND_USAGE_KEY = 'sidekick.nodeCommandUsage';
const productCategoryOrder = { Trigger: 0, Desktop: 1, Window: 2, Hardware: 3, Media: 4, Console: 5, Logic: 6, Data: 7, Utility: 8 };

interface PendingConnection {
  sourceNodeId: string;
  sourcePortId: string;
}

interface NodeCommandUsage {
  count: number;
  lastUsedAt: number;
}

function readNodeCommandUsage(): Record<string, NodeCommandUsage> {
  if (typeof window === 'undefined') {
    return {};
  }

  try {
    const raw = window.localStorage.getItem(NODE_COMMAND_USAGE_KEY);
    if (!raw) {
      return {};
    }

    const parsed = JSON.parse(raw) as Record<string, Partial<NodeCommandUsage>>;
    return Object.entries(parsed).reduce<Record<string, NodeCommandUsage>>((acc, [typeId, usage]) => {
      acc[typeId] = {
        count: Number.isFinite(usage.count) ? Number(usage.count) : 0,
        lastUsedAt: Number.isFinite(usage.lastUsedAt) ? Number(usage.lastUsedAt) : 0,
      };
      return acc;
    }, {});
  } catch {
    return {};
  }
}

function writeNodeCommandUsage(usage: Record<string, NodeCommandUsage>) {
  if (typeof window === 'undefined') {
    return;
  }

  try {
    window.localStorage.setItem(NODE_COMMAND_USAGE_KEY, JSON.stringify(usage));
  } catch {
    // Command history is a convenience only; quota/privacy errors should not block creation.
  }
}

function recordNodeCommandUse(
  usage: Record<string, NodeCommandUsage>,
  typeId: string,
): Record<string, NodeCommandUsage> {
  const previous = usage[typeId] ?? { count: 0, lastUsedAt: 0 };
  const next = {
    ...usage,
    [typeId]: {
      count: previous.count + 1,
      lastUsedAt: Date.now(),
    },
  };
  writeNodeCommandUsage(next);
  return next;
}

export default function FlowCanvas() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const reactFlowInstance = useRef<ReactFlowInstance<Node<FlowNodeData | StickyNoteData>, Edge> | null>(null);
  const connectionCompletedRef = useRef(false);
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    position: { x: number; y: number };
    filter: string;
    pendingConnection?: PendingConnection | null;
    insertEdgeId?: string | null;
  } | null>(null);
  const [nodeContextMenu, setNodeContextMenu] = useState<{
    x: number;
    y: number;
    nodeId: string;
  } | null>(null);
  const [edgeContextMenu, setEdgeContextMenu] = useState<{
    x: number;
    y: number;
    position: { x: number; y: number };
    edgeId: string;
  } | null>(null);

  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const stickyNotes = useFlowStore((s) => s.stickyNotes);
  const applyStickyNoteChange = useFlowStore((s) => s.applyStickyNoteChange);
  const nodeDefinitions = useFlowStore((s) => s.nodeDefinitions);
  const onNodesChangeRaw = useFlowStore((s) => s.onNodesChange);
  const onEdgesChange = useFlowStore((s) => s.onEdgesChange);
  const addNode = useFlowStore((s) => s.addNode);
  const autoLayout = useFlowStore((s) => s.autoLayout);
  const canConnect = useFlowStore((s) => s.canConnect);
  const connectNodes = useFlowStore((s) => s.connectNodes);
  const duplicateNode = useFlowStore((s) => s.duplicateNode);
  const duplicateStickyNote = useFlowStore((s) => s.duplicateStickyNote);
  const insertNodeOnEdge = useFlowStore((s) => s.insertNodeOnEdge);
  const reconnectEdgeById = useFlowStore((s) => s.reconnectEdge);
  const removeEdge = useFlowStore((s) => s.removeEdge);
  const removeNode = useFlowStore((s) => s.removeNode);
  const removeStickyNote = useFlowStore((s) => s.removeStickyNote);
  const selectedNodeId = useFlowStore((s) => s.selectedNodeId);
  const setSelectedNodeId = useFlowStore((s) => s.setSelectedNodeId);
  const toggleNodeDisabled = useFlowStore((s) => s.toggleNodeDisabled);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const addLog = useAppStore((s) => s.addLog);
  const nodeStatuses = useAppStore((s) => s.nodeStatuses);
  const debugVisualEnabled = useAppStore((s) => s.debugVisualEnabled);
  const setInspectorMode = useAppStore((s) => s.setInspectorMode);
  const capturedElement = useAppStore((s) => s.capturedElement);
  const capturedRegion = useAppStore((s) => s.capturedRegion);
  const macroRecorderActive = useAppStore((s) => s.macroRecorderActive);
  const setMacroRecorderActive = useAppStore((s) => s.setMacroRecorderActive);
  const setMacroRecorderStatus = useAppStore((s) => s.setMacroRecorderStatus);
  const guidedDraft = useAppStore((s) => s.guidedDraft);
  const setGuidedDraft = useAppStore((s) => s.setGuidedDraft);
  const [pendingConnection, setPendingConnection] = useState<PendingConnection | null>(null);
  const [commandUsage, setCommandUsage] = useState<Record<string, NodeCommandUsage>>(() => readNodeCommandUsage());
  const selectedNode = nodes.find((node) => node.id === selectedNodeId) ?? null;
  const selectedStickyNote = stickyNotes.find((sticky) => sticky.id === selectedNodeId) ?? null;

  const renderedNodes = useMemo(
    () => [...nodes, ...stickyNotes] as Node<FlowNodeData | StickyNoteData>[],
    [nodes, stickyNotes],
  );

  const renderedEdges = useMemo<Edge[]>(() => edges.map((edge): Edge => {
    const sourceStatus = nodeStatuses[edge.source] ?? 'Idle';
    const targetStatus = nodeStatuses[edge.target] ?? 'Idle';
    const isErrorPath = sourceStatus === 'Error' || targetStatus === 'Error';
    const isActivePath = sourceStatus === 'Running'
      || targetStatus === 'Running'
      || (sourceStatus === 'Completed' && targetStatus === 'Completed')
      || (sourceStatus === 'Completed' && targetStatus !== 'Idle');
    const classes = [
      edge.className,
      isActivePath ? 'flow-edge--active' : '',
      isErrorPath ? 'flow-edge--error' : '',
    ].filter(Boolean).join(' ');

    return {
      ...edge,
      animated: debugVisualEnabled && isActivePath && !isErrorPath,
      className: classes || undefined,
      style: {
        ...(edge.style ?? {}),
        strokeWidth: isActivePath ? 3 : 2,
        stroke: isErrorPath ? '#ef4444' : isActivePath ? '#58a6ff' : (edge.style?.stroke ?? '#6b7280'),
      },
    } as Edge;
  }), [debugVisualEnabled, edges, nodeStatuses]);

  const stickyIdSet = useMemo(() => new Set(stickyNotes.map((s) => s.id)), [stickyNotes]);

  const onNodesChange = useCallback(
    (changes: Parameters<typeof onNodesChangeRaw>[0]) => {
      const stickyChanges: Parameters<typeof onNodesChangeRaw>[0] = [];
      const flowChanges: Parameters<typeof onNodesChangeRaw>[0] = [];
      for (const change of changes) {
        const nodeId = (change as { id?: string }).id;
        if (nodeId && stickyIdSet.has(nodeId)) {
          stickyChanges.push(change);
        } else {
          flowChanges.push(change);
        }
      }
      if (flowChanges.length > 0) onNodesChangeRaw(flowChanges);
      if (stickyChanges.length > 0) {
        const mapped: { id: string; position?: { x: number; y: number }; selected?: boolean }[] = [];
        for (const c of stickyChanges) {
          const ch = c as { type: string; id: string; position?: { x: number; y: number }; selected?: boolean; dimensions?: { width: number; height: number } };
          if (ch.type === 'position' && ch.position) {
            mapped.push({ id: ch.id, position: ch.position });
          } else if (ch.type === 'select') {
            mapped.push({ id: ch.id, selected: ch.selected });
          } else if (ch.type === 'remove') {
            useFlowStore.getState().removeStickyNote(ch.id);
          } else if (ch.type === 'dimensions' && ch.dimensions) {
            useFlowStore.getState().updateStickyNote(ch.id, {
              width: ch.dimensions.width,
              height: ch.dimensions.height,
            });
          }
        }
        if (mapped.length > 0) applyStickyNoteChange(mapped);
      }
    },
    [stickyIdSet, onNodesChangeRaw, applyStickyNoteChange],
  );

  // Deselect when clicking canvas background
  const onPaneClick = useCallback(() => {
    setContextMenu(null);
    setNodeContextMenu(null);
    setEdgeContextMenu(null);
    setSelectedNodeId(null);
  }, [setSelectedNodeId]);

  // Drag-and-drop from palette
  const onDragOver = useCallback((event: DragEvent) => {
    event.preventDefault();
    event.dataTransfer.dropEffect = 'move';
  }, []);

  const onDrop = useCallback(
    (event: DragEvent) => {
      event.preventDefault();

      const typeId = event.dataTransfer.getData('application/ajudante-node');
      if (!typeId) return;

      const bounds = reactFlowWrapper.current?.getBoundingClientRect();
      if (!bounds || !reactFlowInstance.current) return;

      const position = reactFlowInstance.current.screenToFlowPosition({
        x: event.clientX - bounds.left,
        y: event.clientY - bounds.top,
      });

      addNode(typeId, position);
    },
    [addNode],
  );

  const onInit = useCallback((instance: ReactFlowInstance<Node<FlowNodeData | StickyNoteData>, Edge>) => {
    reactFlowInstance.current = instance;
  }, []);

  const getFlowPositionFromEvent = useCallback((event: CanvasPointerEvent | DragEvent) => {
    const bounds = reactFlowWrapper.current?.getBoundingClientRect();
    if (!bounds || !reactFlowInstance.current) {
      return null;
    }

    return reactFlowInstance.current.screenToFlowPosition({
      x: event.clientX - bounds.left,
      y: event.clientY - bounds.top,
    });
  }, []);

  const onPaneContextMenu = useCallback((event: CanvasPointerEvent) => {
    event.preventDefault();
    const position = getFlowPositionFromEvent(event);
    if (!position) {
      return;
    }

    const menuPosition = clampContextMenuPosition(event.clientX, event.clientY);
    setContextMenu({
      x: menuPosition.x,
      y: menuPosition.y,
      position,
      filter: '',
      pendingConnection: null,
      insertEdgeId: null,
    });
    setNodeContextMenu(null);
    setEdgeContextMenu(null);
  }, [getFlowPositionFromEvent]);

  const resolveCompatibleTargetPort = useCallback((typeId: string, sourcePortId: string, sourceNodeId: string) => {
    const definition = nodeDefinitions.find((candidate) => candidate.typeId === typeId);
    const sourceNode = nodes.find((node) => node.id === sourceNodeId);
    const sourcePort = sourceNode?.data.outputPorts.find((port) => port.id === sourcePortId);
    if (!definition || !sourcePort) {
      return null;
    }

    return definition.inputPorts.find((targetPort) =>
      sourcePort.dataType === 'Any'
      || targetPort.dataType === 'Any'
      || sourcePort.dataType === targetPort.dataType) ?? null;
  }, [nodeDefinitions, nodes]);

  const isDefinitionCompatibleWithContext = useCallback((typeId: string) => {
    if (!contextMenu) {
      return true;
    }

    if (contextMenu.pendingConnection) {
      return Boolean(resolveCompatibleTargetPort(
        typeId,
        contextMenu.pendingConnection.sourcePortId,
        contextMenu.pendingConnection.sourceNodeId,
      ));
    }

    if (contextMenu.insertEdgeId) {
      const edge = edges.find((candidate) => candidate.id === contextMenu.insertEdgeId);
      const definition = nodeDefinitions.find((candidate) => candidate.typeId === typeId);
      const sourceNode = nodes.find((node) => node.id === edge?.source);
      const targetNode = nodes.find((node) => node.id === edge?.target);
      const sourcePort = sourceNode?.data.outputPorts.find((port) => port.id === (edge?.sourceHandle ?? 'out'));
      const targetPort = targetNode?.data.inputPorts.find((port) => port.id === (edge?.targetHandle ?? 'in'));
      if (!edge || !definition || !sourcePort || !targetPort) {
        return false;
      }

      const hasCompatibleInput = definition.inputPorts.some((port) =>
        sourcePort.dataType === 'Any'
        || port.dataType === 'Any'
        || sourcePort.dataType === port.dataType);
      const hasCompatibleOutput = definition.outputPorts.some((port) =>
        port.dataType === 'Any'
        || targetPort.dataType === 'Any'
        || port.dataType === targetPort.dataType);
      return hasCompatibleInput && hasCompatibleOutput;
    }

    return true;
  }, [contextMenu, edges, nodeDefinitions, nodes, resolveCompatibleTargetPort]);

  const rememberNodeCommand = useCallback((typeId: string) => {
    setCommandUsage((current) => recordNodeCommandUse(current, typeId));
  }, []);

  const addNodeFromContext = useCallback((typeId: string, propertyOverrides?: Record<string, unknown>) => {
    if (!contextMenu) {
      return;
    }

    if (contextMenu.insertEdgeId) {
      const insertedNodeId = insertNodeOnEdge(contextMenu.insertEdgeId, typeId, contextMenu.position, propertyOverrides);
      if (!insertedNodeId) {
        const message = 'Node nao inserido no fio: portas incompatíveis com a conexão existente.';
        addLog({ timestamp: new Date().toISOString(), level: 'warning', message });
        setUserMessage({ type: 'info', text: message });
      } else {
        rememberNodeCommand(typeId);
      }
      setContextMenu(null);
      return;
    }

    const newNodeId = addNode(typeId, contextMenu.position, propertyOverrides);
    if (newNodeId) {
      rememberNodeCommand(typeId);
    }
    if (newNodeId && contextMenu.pendingConnection) {
      const targetPort = resolveCompatibleTargetPort(
        typeId,
        contextMenu.pendingConnection.sourcePortId,
        contextMenu.pendingConnection.sourceNodeId,
      );
      if (!targetPort) {
        const message = 'Novo node criado sem conexão automática: portas incompatíveis ou node sem entrada.';
        addLog({ timestamp: new Date().toISOString(), level: 'warning', message });
        setUserMessage({ type: 'info', text: message });
      } else {
        const result = connectNodes({
          source: contextMenu.pendingConnection.sourceNodeId,
          sourceHandle: contextMenu.pendingConnection.sourcePortId,
          target: newNodeId,
          targetHandle: targetPort.id,
        });
        if (!result.ok) {
          const message = result.reason ?? 'Conexao falhou por portas incompatíveis.';
          addLog({ timestamp: new Date().toISOString(), level: 'warning', message });
          setUserMessage({ type: 'info', text: message });
        }
      }
    }
    setContextMenu(null);
  }, [addLog, addNode, connectNodes, contextMenu, insertNodeOnEdge, rememberNodeCommand, resolveCompatibleTargetPort, setUserMessage]);

  const filteredDefinitions = useMemo(() => {
    const filter = contextMenu?.filter.trim().toLocaleLowerCase('en-US') ?? '';
    const definitions = filter
      ? nodeDefinitions.filter((definition) => [
        definition.displayName,
        definition.description,
        definition.category,
        definition.typeId,
      ].join(' ').toLocaleLowerCase('en-US').includes(filter))
      : nodeDefinitions;

    return definitions
      .filter((definition) => isDefinitionCompatibleWithContext(definition.typeId))
      .sort((left, right) => {
      const leftUsage = commandUsage[left.typeId];
      const rightUsage = commandUsage[right.typeId];
      const leftScore = (leftUsage?.count ?? 0) * 1000 + (leftUsage?.lastUsedAt ?? 0) / 1_000_000_000;
      const rightScore = (rightUsage?.count ?? 0) * 1000 + (rightUsage?.lastUsedAt ?? 0) / 1_000_000_000;
      if (leftScore !== rightScore) {
        return rightScore - leftScore;
      }

      const leftCategory = getNodeProductCategory(left);
      const rightCategory = getNodeProductCategory(right);
      if (leftCategory !== rightCategory) {
        return productCategoryOrder[leftCategory] - productCategoryOrder[rightCategory];
      }

      return left.displayName.localeCompare(right.displayName);
    });
  }, [commandUsage, contextMenu?.filter, isDefinitionCompatibleWithContext, nodeDefinitions]);

  const recentDefinitions = useMemo(() => filteredDefinitions
    .filter((definition) => commandUsage[definition.typeId]?.count > 0)
    .sort((left, right) => {
      const leftUsage = commandUsage[left.typeId];
      const rightUsage = commandUsage[right.typeId];
      if ((leftUsage?.count ?? 0) !== (rightUsage?.count ?? 0)) {
        return (rightUsage?.count ?? 0) - (leftUsage?.count ?? 0);
      }
      return (rightUsage?.lastUsedAt ?? 0) - (leftUsage?.lastUsedAt ?? 0);
    })
    .slice(0, 5), [commandUsage, filteredDefinitions]);

  const categorizedDefinitions = useMemo(() => {
    const groups = new Map<string, typeof filteredDefinitions>();
    for (const definition of filteredDefinitions) {
      const category = getNodeProductCategory(definition);
      groups.set(category, [...(groups.get(category) ?? []), definition]);
    }

    return Array.from(groups.entries())
      .sort(([left], [right]) => productCategoryOrder[left as keyof typeof productCategoryOrder] - productCategoryOrder[right as keyof typeof productCategoryOrder]);
  }, [filteredDefinitions]);

  const recommendedDefinitions = useMemo(() => {
    if (!contextMenu) {
      return [];
    }

    const ids = new Set<string>();
    if (capturedElement) {
      ids.add('action.desktopClickElement');
      ids.add('action.desktopWaitElement');
      ids.add('action.desktopReadElementText');
    }
    if (capturedRegion) {
      ids.add('action.clickImageMatch');
    }
    if (contextMenu.pendingConnection) {
      ids.add('logic.delay');
      ids.add('logic.ifElse');
      ids.add('action.logMessage');
    }
    if (!contextMenu.pendingConnection && !contextMenu.insertEdgeId) {
      ids.add('trigger.manualStart');
      ids.add('trigger.scheduleTime');
      ids.add('trigger.processEvent');
    }

    return nodeDefinitions.filter((definition) => ids.has(definition.typeId)).slice(0, 6);
  }, [capturedElement, capturedRegion, contextMenu, nodeDefinitions]);

  const miraOverrides = useMemo(() => createMiraSelectorOverrides(capturedElement), [capturedElement]);
  const snipOverrides = useMemo(() => createSnipTemplateOverrides(capturedRegion), [capturedRegion]);

  const defaultEdgeOptions = useMemo(
    () => ({
      type: 'smoothstep' as const,
      style: { stroke: '#6b7280', strokeWidth: 2 },
    }),
    [],
  );

  const startMiraFromEmptyState = useCallback(() => {
    setInspectorMode('mira');
    setUserMessage({ type: 'info', text: 'Mira ativo: clique no elemento que deseja capturar.' });
    void sendCommand('platform', 'startMira', {}).catch((error) => {
      const message = error instanceof Error ? error.message : 'Nao foi possivel iniciar o Mira.';
      setInspectorMode('none');
      setUserMessage({ type: 'error', text: message });
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
    });
  }, [addLog, setInspectorMode, setUserMessage]);

  const startFromScratch = useCallback(() => {
    const manualStart = nodeDefinitions.find((definition) => definition.typeId === 'trigger.manualStart')
      ?? nodeDefinitions.find((definition) => definition.category === 'Trigger');
    if (!manualStart) {
      setUserMessage({ type: 'error', text: 'Nenhum gatilho disponivel para iniciar o flow.' });
      return;
    }

    addNode(manualStart.typeId, { x: 120, y: 120 });
    setUserMessage({ type: 'success', text: 'Inicio manual criado. Conecte o proximo passo pelo handle de saida.' });
  }, [addNode, nodeDefinitions, setUserMessage]);

  const toggleMacroRecorder = useCallback(async () => {
    try {
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
        setUserMessage({ type: 'info', text: 'Recorder global ativo. Use Ctrl+Shift+F12 ou pare aqui para revisar a gravacao.' });
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
      if (draft?.savedInspectionAsset) {
        useAppStore.getState().upsertInspectionAsset(draft.savedInspectionAsset);
      }
      setUserMessage({ type: 'success', text: 'Rascunho gravado. Revise a timeline antes de aplicar ao canvas.' });
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Nao foi possivel controlar o gravador.';
      setMacroRecorderActive(false);
      setMacroRecorderStatus(null);
      setUserMessage({ type: 'error', text: message });
      addLog({ timestamp: new Date().toISOString(), level: 'error', message });
    }
  }, [addLog, macroRecorderActive, setGuidedDraft, setMacroRecorderActive, setMacroRecorderStatus, setUserMessage]);

  const resolveClientPoint = useCallback((event: CanvasPointerLikeEvent): { x: number; y: number } | null => {
    if ('clientX' in event && 'clientY' in event) {
      return { x: event.clientX, y: event.clientY };
    }

    const touch = event.changedTouches?.[0] ?? event.touches?.[0];
    if (!touch) {
      return null;
    }

    return { x: touch.clientX, y: touch.clientY };
  }, []);

  const onConnectStart = useCallback((_: unknown, params: { nodeId?: string | null; handleId?: string | null; handleType?: 'source' | 'target' | null }) => {
    connectionCompletedRef.current = false;
    if (params.handleType !== 'source' || !params.nodeId || !params.handleId) {
      setPendingConnection(null);
      return;
    }

    setPendingConnection({
      sourceNodeId: params.nodeId,
      sourcePortId: params.handleId,
    });
  }, []);

  const handleConnect = useCallback((connection: Connection) => {
    const result = connectNodes(connection);
    connectionCompletedRef.current = result.ok;
    if (!result.ok) {
      const message = result.reason ?? 'Conexao falhou.';
      addLog({ timestamp: new Date().toISOString(), level: 'warning', message });
      setUserMessage({ type: 'info', text: message });
    }
  }, [addLog, connectNodes, setUserMessage]);

  const isValidConnection = useCallback<IsValidConnection<Edge>>((connection) => (
    canConnect({
      source: connection.source,
      sourceHandle: connection.sourceHandle ?? null,
      target: connection.target,
      targetHandle: connection.targetHandle ?? null,
    }).ok
  ), [canConnect]);

  const handleReconnect = useCallback((oldEdge: Edge, newConnection: Connection) => {
    const result = reconnectEdgeById(oldEdge.id, newConnection);
    if (!result.ok) {
      const message = result.reason ?? 'Reconexao falhou.';
      addLog({ timestamp: new Date().toISOString(), level: 'warning', message });
      setUserMessage({ type: 'info', text: message });
    }
  }, [addLog, reconnectEdgeById, setUserMessage]);

  const onNodeContextMenu = useCallback((event: CanvasPointerEvent, node: Node<FlowNodeData | StickyNoteData>) => {
    event.preventDefault();
    const menuPosition = clampContextMenuPosition(event.clientX, event.clientY);
    setSelectedNodeId(node.id);
    setContextMenu(null);
    setEdgeContextMenu(null);
    setNodeContextMenu({
      x: menuPosition.x,
      y: menuPosition.y,
      nodeId: node.id,
    });
  }, [setSelectedNodeId]);

  const onEdgeContextMenu = useCallback((event: CanvasPointerEvent, edge: Edge) => {
    event.preventDefault();
    const position = getFlowPositionFromEvent(event);
    if (!position) {
      return;
    }

    const menuPosition = clampContextMenuPosition(event.clientX, event.clientY);
    setContextMenu(null);
    setNodeContextMenu(null);
    setEdgeContextMenu({
      x: menuPosition.x,
      y: menuPosition.y,
      position,
      edgeId: edge.id,
    });
  }, [getFlowPositionFromEvent]);

  const openInsertMenuForEdge = useCallback(() => {
    if (!edgeContextMenu) {
      return;
    }

    setContextMenu({
      x: edgeContextMenu.x,
      y: edgeContextMenu.y,
      position: edgeContextMenu.position,
      filter: '',
      pendingConnection: null,
      insertEdgeId: edgeContextMenu.edgeId,
    });
    setEdgeContextMenu(null);
  }, [edgeContextMenu]);

  const onConnectEnd = useCallback((event: CanvasPointerLikeEvent) => {
    if (!pendingConnection) {
      return;
    }

    if (connectionCompletedRef.current) {
      connectionCompletedRef.current = false;
      setPendingConnection(null);
      return;
    }

    const clientPoint = resolveClientPoint(event);
    if (!clientPoint) {
      setPendingConnection(null);
      return;
    }

    const rawTarget = event.target;
    const targetElement = rawTarget instanceof Element ? rawTarget : null;
    const blockedTarget = Boolean(targetElement?.closest('.flow-context-menu'));
    const bounds = reactFlowWrapper.current?.getBoundingClientRect();
    const hasUsableBounds = Boolean(bounds && (bounds.width > 0 || bounds.height > 0));
    const droppedInsideCanvas = !hasUsableBounds || (
      clientPoint.x >= bounds!.left
      && clientPoint.x <= bounds!.right
      && clientPoint.y >= bounds!.top
      && clientPoint.y <= bounds!.bottom
    );

    if (!droppedInsideCanvas || blockedTarget) {
      setPendingConnection(null);
      return;
    }

    const panePosition = getFlowPositionFromEvent({
      clientX: clientPoint.x,
      clientY: clientPoint.y,
    } as CanvasPointerEvent);
    if (!panePosition) {
      setPendingConnection(null);
      return;
    }

    const menuPosition = clampContextMenuPosition(clientPoint.x, clientPoint.y);
    setContextMenu({
      x: menuPosition.x,
      y: menuPosition.y,
      position: panePosition,
      filter: '',
      pendingConnection,
      insertEdgeId: null,
    });
    setNodeContextMenu(null);
    setEdgeContextMenu(null);
    setPendingConnection(null);
  }, [getFlowPositionFromEvent, pendingConnection, resolveClientPoint]);

  const openAddMenuAtCenter = useCallback((connection?: PendingConnection | null) => {
    const bounds = reactFlowWrapper.current?.getBoundingClientRect();
    const clientX = bounds ? bounds.left + bounds.width / 2 : window.innerWidth / 2;
    const clientY = bounds ? bounds.top + bounds.height / 2 : window.innerHeight / 2;
    const position = getFlowPositionFromEvent({ clientX, clientY } as CanvasPointerEvent) ?? { x: 0, y: 0 };
    const menuPosition = clampContextMenuPosition(clientX, clientY);
    setContextMenu({
      x: menuPosition.x,
      y: menuPosition.y,
      position,
      filter: '',
      pendingConnection: connection ?? null,
      insertEdgeId: null,
    });
    setNodeContextMenu(null);
    setEdgeContextMenu(null);
  }, [getFlowPositionFromEvent]);

  useEffect(() => {
    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      const target = event.target as HTMLElement | null;
      if (target?.closest('input, textarea, select, [contenteditable="true"]')) {
        return;
      }

      const key = event.key.toLocaleLowerCase('en-US');
      if ((event.ctrlKey || event.metaKey) && key === 'd' && selectedNodeId) {
        event.preventDefault();
        if (selectedStickyNote) {
          duplicateStickyNote(selectedNodeId);
        } else {
          duplicateNode(selectedNodeId);
        }
        return;
      }

      if ((event.ctrlKey || event.metaKey) && event.key === '0') {
        event.preventDefault();
        reactFlowInstance.current?.fitView?.({ padding: 0.2 });
        return;
      }

      if ((event.ctrlKey || event.metaKey) && key === 'k') {
        event.preventDefault();
        openAddMenuAtCenter();
        return;
      }

      if (event.key === '/') {
        event.preventDefault();
        openAddMenuAtCenter();
        return;
      }

      if (key === 'c' && selectedNode) {
        const firstOutput = selectedNode.data.outputPorts[0];
        if (firstOutput) {
          event.preventDefault();
          openAddMenuAtCenter({ sourceNodeId: selectedNode.id, sourcePortId: firstOutput.id });
        }
        return;
      }

      if (key === 'l') {
        event.preventDefault();
        autoLayout();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [autoLayout, duplicateNode, duplicateStickyNote, openAddMenuAtCenter, selectedNode, selectedNodeId, selectedStickyNote]);

  return (
    <div ref={reactFlowWrapper} className="flow-canvas">
      <ReactFlow
        nodes={renderedNodes}
        edges={renderedEdges}
        onNodesChange={onNodesChange as unknown as OnNodesChange<Node<FlowNodeData | StickyNoteData>>}
        onEdgesChange={onEdgesChange}
        onConnect={handleConnect}
        onReconnect={handleReconnect}
        onInit={onInit}
        onPaneClick={onPaneClick}
        onPaneContextMenu={onPaneContextMenu}
        onNodeContextMenu={onNodeContextMenu}
        onEdgeContextMenu={onEdgeContextMenu}
        onDrop={onDrop}
        onDragOver={onDragOver}
        onConnectStart={onConnectStart}
        onConnectEnd={onConnectEnd}
        isValidConnection={isValidConnection}
        nodeTypes={nodeTypes}
        defaultEdgeOptions={defaultEdgeOptions}
        connectionLineStyle={{ stroke: '#58a6ff', strokeWidth: 3 }}
        edgesReconnectable
        fitView
        proOptions={{ hideAttribution: true }}
        colorMode="dark"
        deleteKeyCode="Delete"
        minZoom={0.1}
        maxZoom={3}
        connectionRadius={48}
      >
        <Background variant={BackgroundVariant.Dots} gap={20} size={1} color="#333355" />
        <MiniMap
          nodeStrokeWidth={3}
          style={{
            backgroundColor: '#0d1117',
            border: '1px solid #30363d',
            borderRadius: 8,
          }}
        />
        <Controls
          style={{
            borderRadius: 8,
            border: '1px solid #30363d',
          }}
        />
      </ReactFlow>
      <button
        type="button"
        className="flow-canvas__quick-add"
        onClick={() => openAddMenuAtCenter()}
        title="Adicionar passo (/ ou Ctrl+K)"
      >
        <span>+</span>
        Adicionar passo
        <kbd>/</kbd>
      </button>
      {nodes.length === 0 && stickyNotes.length === 0 && (
        <div className="flow-empty-state" role="region" aria-label="Assistente de Automacao">
          <div className="flow-empty-state__eyebrow">Primeiro sucesso</div>
          <h2 className="flow-empty-state__title">Escolha um caminho seguro para comecar</h2>
          <p className="flow-empty-state__copy">
            Grave uma tarefa real, importe uma receita desarmada, capture com Mira ou monte no canvas. Revise a Saude e rode Dry-run antes de executar.
          </p>
          <div className="flow-empty-state__actions">
            <button type="button" className="flow-empty-state__btn flow-empty-state__btn--primary" onClick={() => { void toggleMacroRecorder(); }}>
              {macroRecorderActive ? 'Parar gravacao' : 'Gravar uma automacao'}
            </button>
            <button
              type="button"
              className="flow-empty-state__btn"
              onClick={() => setUserMessage({ type: 'info', text: 'Abra Receitas na toolbar para importar uma copia desarmada.' })}
            >
              Usar receita
            </button>
            <button type="button" className="flow-empty-state__btn" onClick={startMiraFromEmptyState}>
              Capturar com Mira
            </button>
            <button type="button" className="flow-empty-state__btn" onClick={startFromScratch}>
              Criar manualmente
            </button>
          </div>
          {guidedDraft && (
            <div className="flow-empty-state__draft">
              <span>{guidedDraft.displayName} aberto para revisao</span>
              <button type="button" onClick={() => setUserMessage({ type: 'info', text: 'Revise a timeline no painel Recorder antes de aplicar.' })}>
                Revisar timeline
              </button>
            </div>
          )}
        </div>
      )}
      {nodeContextMenu && (
        <div
          className="flow-mini-menu"
          style={{ left: nodeContextMenu.x, top: nodeContextMenu.y }}
          role="menu"
          aria-label="Node actions"
          onContextMenu={(event) => event.preventDefault()}
        >
          {stickyIdSet.has(nodeContextMenu.nodeId) ? (
            <>
              <button
                type="button"
                className="flow-mini-menu__item"
                onClick={() => {
                  duplicateStickyNote(nodeContextMenu.nodeId);
                  setNodeContextMenu(null);
                }}
              >
                Duplicar sticky
              </button>
              <button
                type="button"
                className="flow-mini-menu__item flow-mini-menu__item--danger"
                onClick={() => {
                  removeStickyNote(nodeContextMenu.nodeId);
                  setNodeContextMenu(null);
                }}
              >
                Remover sticky
              </button>
            </>
          ) : (
            <>
              <button
                type="button"
                className="flow-mini-menu__item"
                onClick={() => {
                  duplicateNode(nodeContextMenu.nodeId);
                  setNodeContextMenu(null);
                }}
              >
                Duplicar node
              </button>
              <button
                type="button"
                className="flow-mini-menu__item"
                onClick={() => {
                  const node = nodes.find((candidate) => candidate.id === nodeContextMenu.nodeId);
                  toggleNodeDisabled(nodeContextMenu.nodeId, !node?.data.nodeDisabled);
                  setNodeContextMenu(null);
                }}
              >
                {nodes.find((node) => node.id === nodeContextMenu.nodeId)?.data.nodeDisabled ? 'Habilitar node' : 'Desabilitar node'}
              </button>
              <button
                type="button"
                className="flow-mini-menu__item flow-mini-menu__item--danger"
                onClick={() => {
                  removeNode(nodeContextMenu.nodeId);
                  setNodeContextMenu(null);
                }}
              >
                Remover node
              </button>
            </>
          )}
        </div>
      )}
      {edgeContextMenu && (
        <div
          className="flow-mini-menu"
          style={{ left: edgeContextMenu.x, top: edgeContextMenu.y }}
          role="menu"
          aria-label="Edge actions"
          onContextMenu={(event) => event.preventDefault()}
        >
          <button type="button" className="flow-mini-menu__item" onClick={openInsertMenuForEdge}>
            Inserir passo no fio
          </button>
          <button
            type="button"
            className="flow-mini-menu__item flow-mini-menu__item--danger"
            onClick={() => {
              removeEdge(edgeContextMenu.edgeId);
              setEdgeContextMenu(null);
            }}
          >
            Remover fio
          </button>
        </div>
      )}
      {contextMenu && (
        <div
          className="flow-context-menu"
          style={{ left: contextMenu.x, top: contextMenu.y }}
          role="menu"
          aria-label="Add automation step"
          onContextMenu={(event) => event.preventDefault()}
        >
          <div className="flow-context-menu__header">
            <div>
              <div className="flow-context-menu__title">Adicionar passo</div>
              <div className="flow-context-menu__subtitle">
                {contextMenu.pendingConnection
                  ? 'Selecione um node para criar e conectar automaticamente'
                  : contextMenu.insertEdgeId
                    ? 'Escolha um node compativel para inserir no fio selecionado'
                  : 'Crie nodes com busca, receitas e capturas recentes'}
              </div>
            </div>
            <button
              type="button"
              className="flow-context-menu__close"
              onClick={() => setContextMenu(null)}
              aria-label="Fechar menu"
            >
              ×
            </button>
          </div>

          {(capturedElement || capturedRegion) && (
            <div className="flow-context-menu__section">
              <div className="flow-context-menu__section-title">Usar captura recente</div>
              {capturedElement && (
                <>
                  <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.desktopClickElement', miraOverrides)}>
                    <span>Clicar alvo da Mira</span>
                    <small>{capturedElement.name || capturedElement.automationId || capturedElement.windowTitle}</small>
                  </button>
                  <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.desktopWaitElement', miraOverrides)}>
                    <span>Aguardar alvo da Mira</span>
                    <small>{capturedElement.processName || 'Desktop selector'}</small>
                  </button>
                  <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.desktopReadElementText', miraOverrides)}>
                    <span>Ler texto da Mira</span>
                    <small>{capturedElement.controlType || 'Element text'}</small>
                  </button>
                </>
              )}
              {capturedRegion && (
                <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.clickImageMatch', snipOverrides)}>
                  <span>Clicar imagem do Snip</span>
                  <small>{capturedRegion.bounds.width} x {capturedRegion.bounds.height} px visual fallback</small>
                </button>
              )}
            </div>
          )}

          {recentDefinitions.length > 0 && (
            <div className="flow-context-menu__section">
              <div className="flow-context-menu__section-title">Recentes e frequentes</div>
              <div className="flow-context-menu__pills">
                {recentDefinitions.map((definition) => (
                  <button
                    key={definition.typeId}
                    type="button"
                    className="flow-context-menu__pill"
                    onClick={() => addNodeFromContext(definition.typeId)}
                  >
                    <span>{definition.displayName}</span>
                    <small>{commandUsage[definition.typeId]?.count ?? 0} uso(s)</small>
                  </button>
                ))}
              </div>
            </div>
          )}

          {recommendedDefinitions.length > 0 && (
            <div className="flow-context-menu__section">
              <div className="flow-context-menu__section-title">Recomendados agora</div>
              {recommendedDefinitions.map((definition) => (
                <button
                  key={definition.typeId}
                  type="button"
                  className="flow-context-menu__quick"
                  onClick={() => addNodeFromContext(definition.typeId)}
                >
                  <span>{definition.displayName}</span>
                  <small>{definition.description}</small>
                </button>
              ))}
            </div>
          )}

          {!contextMenu.pendingConnection && !contextMenu.insertEdgeId && (
            <div className="flow-context-menu__section">
              <div className="flow-context-menu__section-title">Organizacao</div>
              <button
                type="button"
                className="flow-context-menu__quick"
                onClick={() => {
                  autoLayout();
                  setContextMenu(null);
                }}
              >
                <span>Auto layout</span>
                <small>Organiza o fluxo da esquerda para a direita</small>
              </button>
            </div>
          )}

          <div className="flow-context-menu__section">
            <div className="flow-context-menu__section-title">Biblioteca de nodes</div>
            <input
              className="flow-context-menu__search"
              value={contextMenu.filter}
              placeholder="Buscar nodes..."
              autoFocus
              onChange={(event) => setContextMenu({ ...contextMenu, filter: event.target.value })}
              onKeyDown={(event) => {
                if (event.key === 'Escape') {
                  setContextMenu(null);
                }
              }}
            />
            <div className="flow-context-menu__section-title">Categorias</div>
            <div className="flow-context-menu__category-strip" aria-label="Categorias de nodes filtradas">
              {categorizedDefinitions.map(([category, definitions]) => (
                <span key={category} className="flow-context-menu__category-chip">
                  {category}
                  <strong>{definitions.length}</strong>
                </span>
              ))}
            </div>
            <div className="flow-context-menu__list">
              {filteredDefinitions.length === 0 ? (
                <div className="flow-context-menu__empty">Nenhum node encontrado.</div>
              ) : categorizedDefinitions.map(([category, definitions]) => (
                <div key={category} className="flow-context-menu__group">
                  <div className="flow-context-menu__group-title">{category}</div>
                  {definitions.map((definition) => (
                    <button
                      key={definition.typeId}
                      type="button"
                      className="flow-context-menu__item"
                      onClick={() => addNodeFromContext(definition.typeId)}
                    >
                      <span className={`flow-context-menu__dot flow-context-menu__dot--${definition.category.toLowerCase()}`} />
                      <span>
                        <strong>{definition.displayName}</strong>
                        <small>{definition.description}</small>
                      </span>
                    </button>
                  ))}
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function clampContextMenuPosition(x: number, y: number): { x: number; y: number } {
  if (typeof window === 'undefined') {
    return { x, y };
  }

  const maxX = Math.max(8, window.innerWidth - CONTEXT_MENU_WIDTH - 8);
  const maxY = Math.max(8, window.innerHeight - Math.min(CONTEXT_MENU_MAX_HEIGHT, window.innerHeight - 24) - 8);

  return {
    x: Math.min(Math.max(8, x), maxX),
    y: Math.min(Math.max(8, y), maxY),
  };
}
