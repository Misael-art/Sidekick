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
      .find((button) => button.textContent?.includes('Parar'));
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

  it('clears queued runtime work without stopping the active run', async () => {
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
      armedFlowCount: 0,
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
          isArmed: false,
          isRunning: true,
          queuePending: true,
          activeTriggerNodeIds: [],
        },
      ],
    });

    sendCommandMock.mockResolvedValue({
      clearedQueuedRuns: 2,
      remainingQueueLength: 0,
      isRunning: true,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const clearQueueButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Limpar fila'));
    expect(clearQueueButton).toBeTruthy();

    await act(async () => {
      clearQueueButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'clearQueue', {});
    expect(useAppStore.getState().userMessage?.text).toContain('2 item(ns) removido(s)');

    await act(async () => {
      root.unmount();
    });
  });

  it('restarts the current flow by validating and sending restartFlow', async () => {
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

    useAppStore.getState().setRuntimeStatus({
      isRunning: true,
      queueLength: 1,
      armedFlowCount: 0,
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
          isArmed: false,
          isRunning: true,
          queuePending: true,
          activeTriggerNodeIds: [],
        },
      ],
    });

    sendCommandMock.mockImplementation(async (_channel: string, action: string) => {
      if (action === 'securityLint') {
        return {
          isSafeToRun: true,
          issues: [],
          riskLevel: 'low',
          manifestHash: 'OK',
        };
      }

      if (action === 'validateFlow') {
        return {
          isValid: true,
          errors: [],
          warnings: [],
          issues: [],
        };
      }

      if (action === 'restartFlow') {
        return {
          queued: true,
          restarted: true,
          flowId: 'flow-1',
          queueLength: 1,
          queuePending: true,
          cancelledCurrentRun: true,
          clearedQueuedRuns: 1,
          validation: { isValid: true, errors: [], warnings: [], issues: [] },
          security: { isSafeToRun: true, issues: [], riskLevel: 'low', manifestHash: 'OK' },
        };
      }

      return null;
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const restartButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Reiniciar'));
    expect(restartButton).toBeTruthy();

    await act(async () => {
      restartButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'securityLint', expect.any(Object));
    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'validateFlow', expect.any(Object));
    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'restartFlow', expect.any(Object));
    expect(useAppStore.getState().userMessage?.text).toContain('reiniciado');

    await act(async () => {
      root.unmount();
    });
  });

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

    sendCommandMock.mockImplementation(async (_channel: string, action: string) => {
      if (action === 'securityLint') {
        return {
          isSafeToRun: true,
          issues: [],
          riskLevel: 'low',
          manifestHash: 'AB',
        };
      }

      if (action === 'validateFlow') {
        return {
          isValid: false,
          errors: ['Missing trigger'],
          warnings: [],
          issues: [
            { severity: 'error', code: 'flow.trigger.missing', message: 'Missing trigger' },
          ],
        };
      }

      return null;
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const runButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Executar'));
    expect(runButton).toBeTruthy();

    await act(async () => {
      runButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'securityLint', expect.any(Object));
    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'validateFlow', expect.any(Object));
    expect(useAppStore.getState().userMessage?.type).toBe('error');
    expect(useAppStore.getState().userMessage?.text).toContain('Corrija 1 erro(s)');

    await act(async () => {
      root.unmount();
    });
  });

  it('runs dry-run without executing the flow', async () => {
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

    sendCommandMock.mockImplementation(async (_channel: string, action: string) => {
      if (action === 'dryRunFlow') {
        return {
          canRun: true,
          summary: 'Dry-run pronto: nenhuma acao foi executada.',
          steps: [
            { nodeId: 'start', typeId: 'trigger.manualStart', displayName: 'Start', status: 'Ready', requiresConfirmation: false },
          ],
          checkpoints: [],
          validation: { isValid: true, errors: [], warnings: [], issues: [] },
          security: { isSafeToRun: true, issues: [], riskLevel: 'low', manifestHash: 'OK' },
          health: { score: 92, level: 'otimo', canRunWithoutAttention: true, issues: [], suggestions: [] },
        };
      }

      return null;
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const dryRunButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Dry-run'));
    expect(dryRunButton).toBeTruthy();

    await act(async () => {
      dryRunButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(sendCommandMock).toHaveBeenCalledWith('engine', 'dryRunFlow', expect.any(Object));
    expect(useAppStore.getState().userMessage?.text).toContain('Dry-run pronto');
    expect(container.textContent).toContain('Dry-run');
    expect(container.textContent).toContain('Start');
    expect(sendCommandMock).not.toHaveBeenCalledWith('engine', 'runFlow', expect.any(Object));

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
      .find((button) => button.getAttribute('title') === 'Carregar flow');
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

  it('opens a visible recipes catalog with safe local recipes', async () => {
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
          { id: 'recipe-roblox-playtime-limit', name: 'Tempo de Jogo - ROBLOX', isNative: true, nodeCount: 19 },
          { id: 'custom-flow', name: 'Meu Fluxo', isNative: false, nodeCount: 2 },
        ];
      }
      if (action === 'listRecipeCatalog') {
        return [
          {
            id: 'recipe-overlay-visual-message',
            name: 'Recipe - Overlay Visual Message',
            category: 'Visual',
            persona: 'iniciante',
            risk: 'low',
            popularity: 30,
            tags: ['safe', 'overlay'],
          },
          {
            id: 'recipe-roblox-playtime-limit',
            name: 'Tempo de Jogo - ROBLOX',
            category: 'Sistema',
            persona: 'responsavel',
            risk: 'high',
            popularity: 80,
            tags: ['processo', 'tempo'],
          },
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

    const recipesButton = Array.from(container.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('Receitas'));
    expect(recipesButton).toBeTruthy();
    expect(container.querySelector('.toolbar__more-summary')?.textContent).toContain('Avancado');

    await act(async () => {
      recipesButton!.dispatchEvent(new MouseEvent('click', { bubbles: true }));
    });

    expect(container.textContent).toContain('Receitas prontas');
    expect(container.textContent).toContain('Importar copia desarmada');
    expect(container.textContent).toContain('Recipe - Overlay Visual Message');
    expect(container.textContent).toContain('Tempo de Jogo - ROBLOX');
    expect(container.textContent).toContain('high');
    expect(container.textContent).not.toContain('Meu Fluxo');

    await act(async () => {
      root.unmount();
    });
  });

  it('keeps the top-level toolbar focused on product commands and moves file controls to Arquivo', async () => {
    const React = await import('react');
    const { default: Toolbar } = await import('./Toolbar');
    const { useFlowStore } = await import('../../store/flowStore');
    const { useAppStore } = await import('../../store/appStore');

    useFlowStore.setState({
      flowId: 'flow-toolbar-focus',
      flowName: 'Toolbar Focus Flow',
      isDirty: false,
      nodes: [
        {
          id: 'trigger-1',
          type: 'triggerNode',
          position: { x: 0, y: 0 },
          data: {
            typeId: 'trigger.filesystem',
            displayName: 'Arquivo criado',
            nodeAlias: '',
            nodeComment: '',
            category: 'Trigger',
            color: '#EF4444',
            inputPorts: [],
            outputPorts: [{ id: 'triggered', name: 'Triggered', dataType: 'Flow' }],
            properties: [],
            propertyValues: {},
          },
        },
      ],
      edges: [],
      selectedNodeId: null,
    });
    useAppStore.setState({
      isRunning: false,
      queueLength: 0,
      flowRuntimes: {},
      currentRun: null,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(Toolbar));
    });

    const topLevelButtons = Array.from(container.querySelectorAll<HTMLButtonElement>('.toolbar > .toolbar__group > button'))
      .map((button) => button.textContent ?? '');

    expect(topLevelButtons.join('|')).toContain('Receitas');
    expect(topLevelButtons.join('|')).toContain('Saude');
    expect(topLevelButtons.join('|')).toContain('Dry-run');
    expect(topLevelButtons.join('|')).toContain('Executar');
    expect(topLevelButtons.join('|')).toContain('Monitorar');
    expect(topLevelButtons.join('|')).toContain('Recorder');
    expect(topLevelButtons.join('|')).toContain('Mira');
    expect(topLevelButtons.join('|')).toContain('Snip');
    expect(topLevelButtons.join('|')).not.toContain('Novo');
    expect(topLevelButtons.join('|')).not.toContain('Salvar');
    expect(topLevelButtons.join('|')).not.toContain('Carregar');

    const fileMenu = container.querySelector('.toolbar__file-menu');
    expect(fileMenu?.textContent).toContain('Arquivo');
    expect(fileMenu?.textContent).toContain('Novo');
    expect(fileMenu?.textContent).toContain('Salvar');
    expect(fileMenu?.textContent).toContain('Carregar');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

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
