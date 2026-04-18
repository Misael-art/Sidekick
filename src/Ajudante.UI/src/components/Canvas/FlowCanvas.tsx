import { useCallback, useRef, useMemo, type DragEvent } from 'react';
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

const nodeTypes = {
  triggerNode: TriggerNode,
  logicNode: LogicNode,
  actionNode: ActionNode,
};

export default function FlowCanvas() {
  const reactFlowWrapper = useRef<HTMLDivElement>(null);
  const reactFlowInstance = useRef<ReactFlowInstance<Node<FlowNodeData>, Edge> | null>(null);

  const nodes = useFlowStore((s) => s.nodes);
  const edges = useFlowStore((s) => s.edges);
  const onNodesChange = useFlowStore((s) => s.onNodesChange);
  const onEdgesChange = useFlowStore((s) => s.onEdgesChange);
  const onConnect = useFlowStore((s) => s.onConnect);
  const addNode = useFlowStore((s) => s.addNode);
  const setSelectedNodeId = useFlowStore((s) => s.setSelectedNodeId);

  // Deselect when clicking canvas background
  const onPaneClick = useCallback(() => {
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

  const defaultEdgeOptions = useMemo(
    () => ({
      type: 'smoothstep' as const,
      style: { stroke: '#6b7280', strokeWidth: 2 },
    }),
    [],
  );

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
        onDrop={onDrop}
        onDragOver={onDragOver}
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
    </div>
  );
}
