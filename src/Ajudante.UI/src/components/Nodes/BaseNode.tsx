import { memo } from 'react';
import { Handle, Position, type NodeProps, type Node } from '@xyflow/react';
import type { FlowNodeData } from '../../bridge/types';
import { useAppStore } from '../../store/appStore';

type BaseNodeProps = NodeProps<Node<FlowNodeData>>;

const statusColors: Record<string, string> = {
  Idle: '#6b7280',
  Running: '#3b82f6',
  Completed: '#22c55e',
  Error: '#ef4444',
  Skipped: '#a855f7',
};

const statusLabels: Record<string, string> = {
  Idle: '',
  Running: 'Running',
  Completed: 'Done',
  Error: 'Error',
  Skipped: 'Skipped',
};

function BaseNode({ id, data, selected }: BaseNodeProps) {
  const status = useAppStore((s) => s.nodeStatuses[id] ?? 'Idle');

  const headerColor = data.color ?? '#6b7280';

  return (
    <div
      className={`base-node ${selected ? 'base-node--selected' : ''}`}
      style={{
        borderColor: selected ? headerColor : 'transparent',
        boxShadow: selected
          ? `0 0 16px ${headerColor}66`
          : '0 2px 8px rgba(0,0,0,0.4)',
      }}
    >
      {/* Header */}
      <div className="base-node__header" style={{ backgroundColor: headerColor }}>
        <span className="base-node__category">{data.category}</span>
        <span className="base-node__title">{data.displayName}</span>
        {status !== 'Idle' && (
          <span
            className="base-node__status"
            style={{ backgroundColor: statusColors[status] }}
            title={statusLabels[status]}
          />
        )}
      </div>

      {/* Body: ports */}
      <div className="base-node__body">
        {/* Input ports (left) */}
        <div className="base-node__ports base-node__ports--input">
          {data.inputPorts.map((port) => (
            <div key={port.id} className="base-node__port">
              <Handle
                type="target"
                position={Position.Left}
                id={port.id}
                className="base-node__handle base-node__handle--target"
                style={{ top: 'auto' }}
              />
              <span className="base-node__port-label">{port.name}</span>
            </div>
          ))}
        </div>

        {/* Output ports (right) */}
        <div className="base-node__ports base-node__ports--output">
          {data.outputPorts.map((port) => (
            <div key={port.id} className="base-node__port base-node__port--right">
              <span className="base-node__port-label">{port.name}</span>
              <Handle
                type="source"
                position={Position.Right}
                id={port.id}
                className="base-node__handle base-node__handle--source"
                style={{ top: 'auto' }}
              />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default memo(BaseNode);
