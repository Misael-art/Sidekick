// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const sendCommandMock = vi.fn();

vi.mock('../../bridge/bridge', () => ({
  sendCommand: sendCommandMock,
}));

describe('Toolbar runtime controls', () => {
  beforeEach(async () => {
    vi.resetModules();
    sendCommandMock.mockReset();
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  });

  it('opens the queue-aware stop dialog when queued work exists', async () => {
    const React = await import('react');
    const { default: Toolbar } = await import('./Toolbar');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Toolbar Flow',
      isDirty: false,
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });

    useAppStore.getState().setRuntimeStatus({
      isRunning: true,
      queueLength: 2,
      armedFlowCount: 1,
      currentRun: {
        flowId: 'flow-1',
        flowName: 'Toolbar Flow',
        source: 'manual',
        startedAt: '2026-04-24T12:00:00.000Z',
      },
      flows: [
        {
          flowId: 'flow-1',
          flowName: 'Toolbar Flow',
          state: 'running',
          isArmed: true,
          isRunning: true,
          queuePending: true,
          activeTriggerNodeIds: ['trigger-1'],
        },
      ],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const stopButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Stop'));
    expect(stopButton).toBeTruthy();

    await act(async () => {
      stopButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.textContent).toContain('Parar runtime');
    expect(container.textContent).toContain('Parar e limpar fila');
    expect(sendCommandMock).not.toHaveBeenCalled();

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

  it('blocks run requests when validation returns errors', async () => {
    const React = await import('react');
    const { default: Toolbar } = await import('./Toolbar');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Toolbar Flow',
      isDirty: false,
      nodes: [],
      edges: [],
      selectedNodeId: null,
      validationResult: null,
    });

    sendCommandMock.mockResolvedValue({
      isValid: false,
      errors: ['Missing trigger'],
      warnings: [],
      issues: [
        { severity: 'error', code: 'flow.trigger.missing', message: 'Missing trigger' },
      ],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const runButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Run Now'));
    expect(runButton).toBeTruthy();

    await act(async () => {
      runButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(sendCommandMock).toHaveBeenCalledTimes(1);
    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'validateFlow', expect.any(Object));
    expect(useAppStore.getState().userMessage?.type).toBe('error');
    expect(useAppStore.getState().userMessage?.text).toContain('Corrija 1 erro(s)');

    await act(async () => {
      root.unmount();
    });
  });

  it('shows delete button only for non-native automations and deletes on confirm', async () => {
    const React = await import('react');
    const { default: Toolbar } = await import('./Toolbar');
    const { useFlowStore } = await import('../../store/flowStore');

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Toolbar Flow',
      isDirty: false,
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });

    sendCommandMock.mockImplementation(async (_channel: string, action: string) => {
      if (action === 'listFlows') {
        return [
          { id: 'native-flow', name: 'Fluxo Nativo', isNative: true, nodeCount: 1 },
          { id: 'custom-flow', name: 'Fluxo Custom', isNative: false, nodeCount: 2 },
        ];
      }
      if (action === 'deleteFlow') {
        return { success: true };
      }
      return null;
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const loadButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.getAttribute('title') === 'Load Flow');
    expect(loadButton).toBeTruthy();

    await act(async () => {
      loadButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    const trashButtonsBefore = Array.from(container.querySelectorAll('.toolbar__flow-delete-btn'));
    expect(trashButtonsBefore).toHaveLength(1);

    await act(async () => {
      trashButtonsBefore[0].dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(confirmSpy).toHaveBeenCalledOnce();
    expect(sendCommandMock).toHaveBeenCalledWith('flow', 'deleteFlow', { flowId: 'custom-flow' });

    confirmSpy.mockRestore();
    await act(async () => {
      root.unmount();
    });
  });

  it('opens a visible marketplace with local recipes', async () => {
    const React = await import('react');
    const { default: Toolbar } = await import('./Toolbar');
    const { useFlowStore } = await import('../../store/flowStore');

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Toolbar Flow',
      isDirty: false,
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });

    sendCommandMock.mockImplementation(async (_channel: string, action: string) => {
      if (action === 'listFlows') {
        return [
          { id: 'recipe-overlay-visual-message', name: 'Recipe - Overlay Visual Message', isNative: true, nodeCount: 3 },
          { id: 'custom-flow', name: 'Meu Fluxo', isNative: false, nodeCount: 2 },
        ];
      }
      return null;
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const marketplaceButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Marketplace'));
    expect(marketplaceButton).toBeTruthy();

    await act(async () => {
      marketplaceButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.textContent).toContain('Receitas prontas');
    expect(container.textContent).toContain('Recipe - Overlay Visual Message');
    expect(container.textContent).not.toContain('Meu Fluxo');

    await act(async () => {
      root.unmount();
    });
  });

  it('lets the user choose PT-BR or English explicitly', async () => {
    const React = await import('react');
    const { default: Toolbar } = await import('./Toolbar');
    const { useFlowStore } = await import('../../store/flowStore');
    const { useLocaleStore } = await import('../../i18n');

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Toolbar Flow',
      isDirty: false,
      nodes: [],
      edges: [],
      selectedNodeId: null,
    });
    useLocaleStore.getState().setLocale('pt-BR');

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const localeSelect = container.querySelector<HTMLSelectElement>('.toolbar__locale-select');
    expect(localeSelect).toBeTruthy();
    expect(Array.from(localeSelect!.options).map((option) => option.value)).toEqual(['pt-BR', 'en']);

    await act(async () => {
      localeSelect!.value = 'en';
      localeSelect!.dispatchEvent(new Event('change', { bubbles: true }));
    });

    expect(useLocaleStore.getState().locale).toBe('en');

    await act(async () => {
      root.unmount();
    });
  });
});
