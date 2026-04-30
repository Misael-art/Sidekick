import { describe, expect, it } from 'vitest';
import { resolveNodeDefinitionsForUi } from './nodeDefinitionFallback';

describe('resolveNodeDefinitionsForUi', () => {
  it('uses bundled product definitions when the host registry returns no nodes', () => {
    const result = resolveNodeDefinitionsForUi([]);

    expect(result.usedFallback).toBe(true);
    expect(result.definitions.length).toBeGreaterThan(0);
    expect(result.definitions.map((definition) => definition.typeId)).toContain('action.desktopClickElement');
    expect(result.definitions.map((definition) => definition.typeId)).toContain('trigger.desktopElementAppeared');
  });

  it('keeps host definitions when they are available', () => {
    const hostDefinitions = [
      {
        typeId: 'action.hostOnly',
        displayName: 'Host Only',
        category: 'Action' as const,
        description: 'Provided by backend',
        color: '#22C55E',
        inputPorts: [],
        outputPorts: [],
        properties: [],
      },
    ];

    const result = resolveNodeDefinitionsForUi(hostDefinitions);

    expect(result.usedFallback).toBe(false);
    expect(result.definitions).toBe(hostDefinitions);
  });
});
