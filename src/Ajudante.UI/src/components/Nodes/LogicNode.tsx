import { memo } from 'react';
import type { NodeProps, Node } from '@xyflow/react';
import type { FlowNodeData } from '../../bridge/types';
import BaseNode from './BaseNode';

type LogicNodeProps = NodeProps<Node<FlowNodeData>>;

function LogicNode(props: LogicNodeProps) {
  const data = { ...props.data, color: props.data.color || '#EAB308' };
  return <BaseNode {...props} data={data} />;
}

export default memo(LogicNode);
