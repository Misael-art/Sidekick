import { memo } from 'react';
import type { NodeProps, Node } from '@xyflow/react';
import type { FlowNodeData } from '../../bridge/types';
import BaseNode from './BaseNode';

type TriggerNodeProps = NodeProps<Node<FlowNodeData>>;

function TriggerNode(props: TriggerNodeProps) {
  const data = { ...props.data, color: props.data.color || '#EF4444' };
  return <BaseNode {...props} data={data} />;
}

export default memo(TriggerNode);
