import type { NodeDefinition } from '../bridge/types';
import { getDevNodeDefinitions } from '../devNodeDefinitions';

export interface NodeDefinitionResolution {
  definitions: NodeDefinition[];
  usedFallback: boolean;
}

export function resolveNodeDefinitionsForUi(
  hostDefinitions: NodeDefinition[] | null | undefined,
): NodeDefinitionResolution {
  if (Array.isArray(hostDefinitions) && hostDefinitions.length > 0) {
    return {
      definitions: hostDefinitions,
      usedFallback: false,
    };
  }

  return {
    definitions: getDevNodeDefinitions(),
    usedFallback: true,
  };
}
