// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it } from 'vitest';
import type { NodeDefinition } from '../../bridge/types';

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

describe('NodePalette Core and Labs presentation', () => {
  beforeEach(async () => {
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    const { useFlowStore } = await import('../../store/flowStore');
    const { useAppStore } = await import('../../store/appStore');

    useFlowStore.setState({
      nodeDefinitions: [
        definition('trigger.hotkey', 'Hotkey Trigger', 'Dispara com atalho global'),
        definition('action.desktopClickElement', 'Desktop Click Element', 'Clica alvo capturado com Mira'),
        definition('action.consoleCommand', 'Console Command', 'Executa comandos avancados'),
        definition('action.systemPower', 'System Power', 'Desliga ou reinicia o Windows'),
      ],
    });
    useAppStore.setState({ isPaletteOpen: true });
  });

  it('shows Core first and keeps Labs collapsed by default', async () => {
    const React = await import('react');
    const { default: NodePalette } = await import('./NodePalette');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(NodePalette));
    });

    const sectionTitles = Array.from(container.querySelectorAll('.node-palette__section-title'))
      .map((element) => element.textContent?.trim());
    expect(sectionTitles[0]).toBe('Core');
    expect(sectionTitles[1]).toBe('Labs / Avancado');
    expect(container.textContent).toContain('Hotkey Trigger');
    expect(container.textContent).toContain('Desktop Click Element');
    expect(container.querySelector('.node-palette__labs-teaser')?.textContent).toContain('2 avancado');

    const visibleItems = Array.from(container.querySelectorAll('.node-palette__item-name'))
      .map((element) => element.textContent);
    expect(visibleItems).not.toContain('Console Command');
    expect(visibleItems).not.toContain('System Power');

    await act(async () => {
      root.unmount();
    });
  });

  it('finds Labs nodes through search even while Labs is collapsed', async () => {
    const React = await import('react');
    const { default: NodePalette } = await import('./NodePalette');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(NodePalette));
    });

    const searchInput = container.querySelector<HTMLInputElement>('.node-palette__input');
    expect(searchInput).toBeTruthy();

    await act(async () => {
      searchInput!.value = 'console';
      searchInput!.dispatchEvent(new Event('input', { bubbles: true }));
    });

    const visibleItems = Array.from(container.querySelectorAll('.node-palette__item-name'))
      .map((element) => element.textContent);
    expect(visibleItems).toEqual(['Console Command']);
    expect(container.textContent).toContain('Labs / Avancado');

    await act(async () => {
      root.unmount();
    });
  });
});
