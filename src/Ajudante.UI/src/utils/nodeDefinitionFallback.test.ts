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

  it('keeps bundled fallback definitions aligned with closed-choice backend nodes', () => {
    const result = resolveNodeDefinitionsForUi([]);
    const mouseClick = result.definitions.find((definition) => definition.typeId === 'action.mouseClick');
    const keyboardPress = result.definitions.find((definition) => definition.typeId === 'action.keyboardPress');

    expect(mouseClick?.properties.find((property) => property.id === 'button')).toMatchObject({
      type: 'Dropdown',
      options: ['Left', 'Right', 'Middle'],
    });
    expect(mouseClick?.properties.find((property) => property.id === 'clickType')).toMatchObject({
      type: 'Dropdown',
      options: ['Single', 'Double'],
    });
    expect(mouseClick?.properties.some((property) => property.id === 'clicks')).toBe(false);
    expect(keyboardPress?.properties.find((property) => property.id === 'key')).toMatchObject({
      type: 'Dropdown',
    });
    expect(keyboardPress?.properties.find((property) => property.id === 'modifiers')).toMatchObject({
      type: 'Dropdown',
      options: ['None', 'Ctrl', 'Shift', 'Alt'],
    });
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

  it('normalizes camelCase enum values from the WPF host registry', () => {
    const result = resolveNodeDefinitionsForUi([
      {
        typeId: 'action.mouseClick',
        displayName: 'Mouse Click',
        category: 'action',
        description: 'Provided by backend',
        color: '#22C55E',
        inputPorts: [{ id: 'in', name: 'In', dataType: 'flow' }],
        outputPorts: [{ id: 'out', name: 'Out', dataType: 'flow' }],
        properties: [
          { id: 'x', name: 'X', type: 'integer', defaultValue: 0 },
          {
            id: 'button',
            name: 'Button',
            type: 'dropdown',
            defaultValue: 'Left',
            options: ['Left', 'Right', 'Middle'],
          },
          {
            id: 'template',
            name: 'Template',
            type: 'imageTemplate',
            defaultValue: '',
          },
        ],
      } as never,
    ]);

    expect(result.usedFallback).toBe(false);
    expect(result.definitions[0].category).toBe('Action');
    expect(result.definitions[0].inputPorts[0].dataType).toBe('Flow');
    expect(result.definitions[0].properties[0].type).toBe('Integer');
    expect(result.definitions[0].properties[1].type).toBe('Dropdown');
    expect(result.definitions[0].properties[2].type).toBe('ImageTemplate');
  });
});
