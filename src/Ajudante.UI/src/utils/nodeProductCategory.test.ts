import { describe, expect, it } from 'vitest';
import { getNodeCapabilityTier, getNodeProductCategory, getProductCategoryOrder } from './nodeProductCategory';
import type { NodeDefinition } from '../bridge/types';

function definition(typeId: string, displayName: string, description = ''): NodeDefinition {
  return {
    typeId,
    displayName,
    description,
    category: typeId.startsWith('trigger.') ? 'Trigger' : typeId.startsWith('logic.') ? 'Logic' : 'Action',
    color: '#22C55E',
    inputPorts: [],
    outputPorts: [],
    properties: [],
  };
}

describe('nodeProductCategory', () => {
  it('maps registry node types into product-facing palette groups', () => {
    expect(getNodeProductCategory(definition('trigger.hotkey', 'Hotkey Trigger'))).toBe('Trigger');
    expect(getNodeProductCategory(definition('action.desktopClickElement', 'Desktop Click Element'))).toBe('Desktop');
    expect(getNodeProductCategory(definition('action.windowControl', 'Window Control'))).toBe('Window');
    expect(getNodeProductCategory(definition('action.systemAudio', 'System Audio'))).toBe('Hardware');
    expect(getNodeProductCategory(definition('action.recordDesktop', 'Record Desktop'))).toBe('Media');
    expect(getNodeProductCategory(definition('action.consoleCommand', 'Console Command'))).toBe('Console');
    expect(getNodeProductCategory(definition('logic.conditionGroup', 'Condition Group'))).toBe('Logic');
    expect(getNodeProductCategory(definition('action.jsonExtract', 'JSON Extract'))).toBe('Data');
    expect(getNodeProductCategory(definition('action.showNotification', 'Show Notification'))).toBe('Utility');
  });

  it('orders categories for quick visual scanning', () => {
    expect(getProductCategoryOrder()).toEqual([
      'Trigger',
      'Desktop',
      'Window',
      'Hardware',
      'Media',
      'Console',
      'Logic',
      'Data',
      'Utility',
    ]);
  });

  it('separates everyday automation nodes from advanced labs capabilities', () => {
    expect(getNodeCapabilityTier(definition('trigger.hotkey', 'Hotkey Trigger'))).toBe('Core');
    expect(getNodeCapabilityTier(definition('trigger.interval', 'Interval'))).toBe('Core');
    expect(getNodeCapabilityTier(definition('action.desktopClickElement', 'Desktop Click Element'))).toBe('Core');
    expect(getNodeCapabilityTier(definition('action.showNotification', 'Show Notification'))).toBe('Core');
    expect(getNodeCapabilityTier(definition('action.systemPower', 'System Power'))).toBe('Labs');
    expect(getNodeCapabilityTier(definition('action.installApp', 'Install App'))).toBe('Labs');
    expect(getNodeCapabilityTier(definition('action.consoleCommand', 'Console Command'))).toBe('Labs');
    expect(getNodeCapabilityTier(definition('action.pdfMerge', 'PDF Merge'))).toBe('Labs');
    expect(getNodeCapabilityTier(definition('action.killProcess', 'Kill Process'))).toBe('Labs');
  });
});
