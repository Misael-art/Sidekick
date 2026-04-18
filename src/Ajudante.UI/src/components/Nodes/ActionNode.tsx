import { memo } from 'react';
import type { NodeProps, Node } from '@xyflow/react';
import type { FlowNodeData } from '../../bridge/types';
import BaseNode from './BaseNode';

type ActionNodeProps = NodeProps<Node<FlowNodeData>>;

function ActionNode(props: ActionNodeProps) {
  const data = { ...props.data, color: props.data.color || '#22C55E' };
  return <BaseNode {...props} data={data} />;
}

export default memo(ActionNode);
