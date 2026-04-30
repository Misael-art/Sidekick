import { useCallback, useRef, useMemo, useState, type DragEvent, type MouseEvent as ReactMouseEvent } from 'react';
import {
  ReactFlow,
  MiniMap,
  Controls,
  Background,
  BackgroundVariant,
  type ReactFlowInstance,
  type Node,
  type Edge,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import type { FlowNodeData } from '../../bridge/types';
import TriggerNode from '../Nodes/TriggerNode';
import LogicNode from '../Nodes/LogicNode';
import ActionNode from '../Nodes/ActionNode';
import { useFlowStore } from '../../store/flowStore';
import { useAppStore } from '../../store/appStore';
import { createMiraSelectorOverrides, createSnipTemplateOverrides } from '../../utils/captureNodePrefill';

const nodeTypes = {
  triggerNode: TriggerNode,
  logicNode: LogicNode,
  actionNode: ActionNode,
};

type CanvasPointerEvent = ReactMouseEvent<Element> | globalThis.MouseEvent;
type CanvasPointerLikeEvent = CanvasPointerEvent | globalThis.TouchEvent;

interface PendingConnection {
  sourceNodeId: string;
  sourcePortId: string;
}

export default function FlowCanvas() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const reactFlowInstance = useRef<ReactFlowInstance<Node<FlowNodeData>, Edge> | null>(null);
  const [contextMenu, setContextMenu] = useState<{
    x: number;
    y: number;
    position: { x: number; y: number };
    filter: string;
    pendingConnection?: PendingConnection | null;
  } | null>(null);

  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const nodeDefinitions = useFlowStore((s) => s.nodeDefinitions);
  const onNodesChange = useFlowStore((s) => s.onNodesChange);
  const onEdgesChange = useFlowStore((s) => s.onEdgesChange);
  const onConnect = useFlowStore((s) => s.onConnect);
  const addNode = useFlowStore((s) => s.addNode);
  const setSelectedNodeId = useFlowStore((s) => s.setSelectedNodeId);
  const setUserMessage = useAppStore((s) => s.setUserMessage);
  const addLog = useAppStore((s) => s.addLog);
  const capturedElement = useAppStore((s) => s.capturedElement);
  const capturedRegion = useAppStore((s) => s.capturedRegion);
  const [pendingConnection, setPendingConnection] = useState<PendingConnection | null>(null);

  // Deselect when clicking canvas background
  const onPaneClick = useCallback(() => {
    setContextMenu(null);
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

  const onInit = useCallback((instance: ReactFlowInstance<Node<FlowNodeData>, Edge>) => {
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

    setContextMenu({
      x: event.clientX,
      y: event.clientY,
      position,
      filter: '',
      pendingConnection: null,
    });
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

  const addNodeFromContext = useCallback((typeId: string, propertyOverrides?: Record<string, unknown>) => {
    if (!contextMenu) {
      return;
    }

    const newNodeId = addNode(typeId, contextMenu.position, propertyOverrides);
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
        onConnect({
          source: contextMenu.pendingConnection.sourceNodeId,
          sourceHandle: contextMenu.pendingConnection.sourcePortId,
          target: newNodeId,
          targetHandle: targetPort.id,
        });
      }
    }
    setContextMenu(null);
  }, [addLog, addNode, contextMenu, onConnect, resolveCompatibleTargetPort, setUserMessage]);

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

    return definitions.sort((left, right) => {
      if (left.category !== right.category) {
        const order = { Trigger: 0, Logic: 1, Action: 2 };
        return order[left.category] - order[right.category];
      }

      return left.displayName.localeCompare(right.displayName);
    });
  }, [contextMenu?.filter, nodeDefinitions]);

  const miraOverrides = useMemo(() => createMiraSelectorOverrides(capturedElement), [capturedElement]);
  const snipOverrides = useMemo(() => createSnipTemplateOverrides(capturedRegion), [capturedRegion]);

  const defaultEdgeOptions = useMemo(
    () => ({
      type: 'smoothstep' as const,
      style: { stroke: '#6b7280', strokeWidth: 2 },
    }),
    [],
  );

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
    if (params.handleType !== 'source' || !params.nodeId || !params.handleId) {
      setPendingConnection(null);
      return;
    }

    setPendingConnection({
      sourceNodeId: params.nodeId,
      sourcePortId: params.handleId,
    });
  }, []);

  const onConnectEnd = useCallback((event: CanvasPointerLikeEvent) => {
    if (!pendingConnection) {
      return;
    }

    const rawTarget = event.target;
    const targetElement = rawTarget instanceof Element ? rawTarget : null;
    const droppedOnPane = Boolean(targetElement?.closest('.react-flow__pane'));
    const droppedOnHandle = Boolean(targetElement?.closest('.react-flow__handle'));
    if (!droppedOnPane || droppedOnHandle) {
      setPendingConnection(null);
      return;
    }

    const clientPoint = resolveClientPoint(event);
    if (!clientPoint) {
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

    setContextMenu({
      x: clientPoint.x,
      y: clientPoint.y,
      position: panePosition,
      filter: '',
      pendingConnection,
    });
    setPendingConnection(null);
  }, [getFlowPositionFromEvent, pendingConnection, resolveClientPoint]);

  return (
    <div ref={reactFlowWrapper} className="flow-canvas">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        onInit={onInit}
        onPaneClick={onPaneClick}
        onPaneContextMenu={onPaneContextMenu}
        onDrop={onDrop}
        onDragOver={onDragOver}
        onConnectStart={onConnectStart}
        onConnectEnd={onConnectEnd}
        nodeTypes={nodeTypes}
        defaultEdgeOptions={defaultEdgeOptions}
        fitView
        proOptions={{ hideAttribution: true }}
        colorMode="dark"
        deleteKeyCode="Delete"
        minZoom={0.1}
        maxZoom={3}
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
              <div className="flow-context-menu__title">Add automation step</div>
              <div className="flow-context-menu__subtitle">
                {contextMenu.pendingConnection
                  ? 'Selecione um node para criar e conectar automaticamente'
                  : 'Right-click menu for building flows faster'}
              </div>
            </div>
            <button
              type="button"
              className="flow-context-menu__close"
              onClick={() => setContextMenu(null)}
              aria-label="Close add menu"
            >
              ×
            </button>
          </div>

          {(capturedElement || capturedRegion) && (
            <div className="flow-context-menu__section">
              <div className="flow-context-menu__section-title">From latest capture</div>
              {capturedElement && (
                <>
                  <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.desktopClickElement', miraOverrides)}>
                    <span>Click latest Mira target</span>
                    <small>{capturedElement.name || capturedElement.automationId || capturedElement.windowTitle}</small>
                  </button>
                  <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.desktopWaitElement', miraOverrides)}>
                    <span>Wait for latest Mira target</span>
                    <small>{capturedElement.processName || 'Desktop selector'}</small>
                  </button>
                  <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.desktopReadElementText', miraOverrides)}>
                    <span>Read latest Mira text</span>
                    <small>{capturedElement.controlType || 'Element text'}</small>
                  </button>
                </>
              )}
              {capturedRegion && (
                <button type="button" className="flow-context-menu__quick" onClick={() => addNodeFromContext('action.clickImageMatch', snipOverrides)}>
                  <span>Click latest Snip image</span>
                  <small>{capturedRegion.bounds.width} x {capturedRegion.bounds.height} px visual fallback</small>
                </button>
              )}
            </div>
          )}

          <div className="flow-context-menu__section">
            <div className="flow-context-menu__section-title">Node library</div>
            <input
              className="flow-context-menu__search"
              value={contextMenu.filter}
              placeholder="Search nodes..."
              autoFocus
              onChange={(event) => setContextMenu({ ...contextMenu, filter: event.target.value })}
              onKeyDown={(event) => {
                if (event.key === 'Escape') {
                  setContextMenu(null);
                }
              }}
            />
            <div className="flow-context-menu__list">
              {filteredDefinitions.length === 0 ? (
                <div className="flow-context-menu__empty">No matching nodes.</div>
              ) : filteredDefinitions.map((definition) => (
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
          </div>
        </div>
      )}
    </div>
  );
}
