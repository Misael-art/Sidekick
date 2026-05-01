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
      definitions: normalizeNodeDefinitions(hostDefinitions),
      usedFallback: false,
    };
  }

  return {
    definitions: getDevNodeDefinitions(),
    usedFallback: true,
  };
}

const categoryMap = {
  trigger: 'Trigger',
  logic: 'Logic',
  action: 'Action',
} as const;

const portDataTypeMap = {
  flow: 'Flow',
  string: 'String',
  number: 'Number',
  boolean: 'Boolean',
  point: 'Point',
  image: 'Image',
  any: 'Any',
} as const;

const propertyTypeMap = {
  string: 'String',
  integer: 'Integer',
  float: 'Float',
  boolean: 'Boolean',
  filepath: 'FilePath',
  folderpath: 'FolderPath',
  hotkey: 'Hotkey',
  point: 'Point',
  color: 'Color',
  dropdown: 'Dropdown',
  imagetemplate: 'ImageTemplate',
} as const;

function normalizeEnumValue<T extends string>(
  value: unknown,
  map: Record<string, T>,
): unknown {
  if (typeof value !== 'string') {
    return value;
  }

  return map[value.toLocaleLowerCase('en-US')] ?? value;
}

export function normalizeNodeDefinitions(definitions: NodeDefinition[]): NodeDefinition[] {
  let changed = false;

  const normalized = definitions.map((definition) => {
    let definitionChanged = false;
    const category = normalizeEnumValue(definition.category, categoryMap);
    if (category !== definition.category) {
      definitionChanged = true;
    }

    const inputPorts = definition.inputPorts.map((port) => {
      const dataType = normalizeEnumValue(port.dataType, portDataTypeMap);
      if (dataType === port.dataType) {
        return port;
      }
      definitionChanged = true;
      return { ...port, dataType } as typeof port;
    });
    const outputPorts = definition.outputPorts.map((port) => {
      const dataType = normalizeEnumValue(port.dataType, portDataTypeMap);
      if (dataType === port.dataType) {
        return port;
      }
      definitionChanged = true;
      return { ...port, dataType } as typeof port;
    });
    const properties = definition.properties.map((property) => {
      const type = normalizeEnumValue(property.type, propertyTypeMap);
      if (type === property.type) {
        return property;
      }
      definitionChanged = true;
      return { ...property, type } as typeof property;
    });

    if (!definitionChanged) {
      return definition;
    }

    changed = true;

    return {
      ...definition,
      category,
      inputPorts,
      outputPorts,
      properties,
    } as NodeDefinition;
  });

  return changed ? normalized : definitions;
}
