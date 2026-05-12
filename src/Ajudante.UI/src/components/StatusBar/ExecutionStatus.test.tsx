// @vitest-environment jsdom

import { act } from 'react';
import { createRoot } from 'react-dom/client';
import { beforeEach, describe, expect, it, vi } from 'vitest';

describe('ExecutionStatus', () => {
  beforeEach(async () => {
    vi.resetModules();
    document.body.innerHTML = '';
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
  });

  it('renders armed flows and runtime metrics', async () => {
    const React = await import('react');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');
    const { default: ExecutionStatus } = await import('./ExecutionStatus');

    useFlowStore.setState({
      flowId: 'flow-1',
      flowName: 'Editor Flow',
      isDirty: false,
      nodes: [],
      edges: [],
      selectedNodeId: null,
      validationResult: {
        isValid: false,
        errors: ['Missing trigger'],
        warnings: ['Unused node'],
        issues: [
          { severity: 'error', code: 'flow.trigger.missing', message: 'Missing trigger' },
          { severity: 'warning', code: 'node.unused', message: 'Unused node' },
        ],
      },
    });

    useAppStore.getState().setRuntimeStatus({
      isRunning: true,
      queueLength: 1,
      armedFlowCount: 2,
      currentRun: {
        flowId: 'flow-2',
        flowName: 'Runtime Flow',
        source: 'trigger',
        startedAt: '2026-04-24T12:00:00.000Z',
      },
      flows: [
        {
          flowId: 'flow-1',
          flowName: 'Editor Flow',
          state: 'armed',
          isArmed: true,
          isRunning: false,
          queuePending: false,
          activeTriggerNodeIds: ['trigger-editor'],
          lastRunAt: '2026-04-24T11:50:00.000Z',
        },
        {
          flowId: 'flow-2',
          flowName: 'Runtime Flow',
          state: 'running',
          isArmed: true,
          isRunning: true,
          queuePending: true,
          activeTriggerNodeIds: ['trigger-runtime'],
          lastTriggerAt: '2026-04-24T11:59:00.000Z',
        },
      ],
    });
    useAppStore.getState().setExecutionHistory([
      {
        runId: 'run-1',
        flowId: 'flow-1',
        flowName: 'Editor Flow',
        source: 'manual',
        startedAt: '2026-04-24T11:45:00.000Z',
        finishedAt: '2026-04-24T11:46:00.000Z',
        result: 'completed',
        logs: [{ timestamp: '2026-04-24T11:45:10.000Z', level: 'info', message: 'Started' }],
        nodeStatuses: [],
      },
    ]);

    useAppStore.setState({ isLogsExpanded: true });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(ExecutionStatus));
    });

    expect(container.textContent).toContain('Editor Flow');
    expect(container.textContent).toContain('Runtime Flow');
    expect(container.textContent).toContain('Queue depth: 1');
    expect(container.textContent).toContain('1 trigger(s)');
    expect(container.textContent).toContain('Blocked by errors');
    expect(container.textContent).toContain('Errors: 1');
    expect(container.textContent).toContain('Warnings: 1');
    expect(container.textContent).toContain('Missing trigger');
    expect(container.textContent).toContain('Recent run');
    expect(container.textContent).toContain('Completed');
    expect(container.textContent).toContain('1 log');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

  it('announces user messages to assistive technology', async () => {
    const React = await import('react');
    const { useAppStore } = await import('../../store/appStore');
    const { default: ExecutionStatus } = await import('./ExecutionStatus');

    useAppStore.setState({
      userMessage: { type: 'success', text: 'Fluxo salvo com sucesso.' },
      isLogsExpanded: false,
      logs: [],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(ExecutionStatus));
    });

    const message = container.querySelector('.exec-status__message');
    expect(message).toBeTruthy();
    expect(message?.getAttribute('role')).toBe('status');
    expect(message?.getAttribute('aria-live')).toBe('polite');
    expect(message?.textContent).toContain('Fluxo salvo com sucesso.');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

  it('shows a pedagogical debug overlay when visual debug is enabled', async () => {
    const React = await import('react');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');
    const { default: ExecutionStatus } = await import('./ExecutionStatus');

    useFlowStore.setState({
      flowId: 'flow-debug',
      flowName: 'Debug Flow',
      validationResult: null,
    });
    useAppStore.setState({
      debugVisualEnabled: true,
      currentRun: {
        flowId: 'flow-debug',
        flowName: 'Debug Flow',
        source: 'manual',
        startedAt: '2026-05-11T12:00:00.000Z',
      },
      runtimeStatus: {
        isRunning: true,
        queueLength: 0,
        armedFlowCount: 0,
        currentRun: {
          flowId: 'flow-debug',
          flowName: 'Debug Flow',
          source: 'manual',
          startedAt: '2026-05-11T12:00:00.000Z',
        },
        flows: [],
      },
      nodeStatusTimeline: [
        { at: '2026-05-11T12:00:02.000Z', nodeId: 'click-send', status: 'Running' },
      ],
      logs: [],
      isLogsExpanded: false,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(ExecutionStatus));
    });

    const overlay = container.querySelector('.exec-status__debug-overlay');
    expect(overlay).toBeTruthy();
    expect(overlay?.textContent).toContain('Debug pedagogico');
    expect(overlay?.textContent).toContain('click-send');
    expect(overlay?.textContent).toContain('Running');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

  it('explains the current runtime phase, fallback and suggested next action', async () => {
    const React = await import('react');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');
    const { default: ExecutionStatus } = await import('./ExecutionStatus');

    useFlowStore.setState({
      flowId: 'flow-debug',
      flowName: 'Debug Flow',
      validationResult: null,
    });
    useAppStore.setState({
      debugVisualEnabled: true,
      currentRun: {
        flowId: 'flow-debug',
        flowName: 'Debug Flow',
        source: 'manual',
        startedAt: '2026-05-12T12:00:00.000Z',
      },
      runtimeStatus: {
        isRunning: true,
        queueLength: 1,
        armedFlowCount: 0,
        currentRun: {
          flowId: 'flow-debug',
          flowName: 'Debug Flow',
          source: 'manual',
          startedAt: '2026-05-12T12:00:00.000Z',
        },
        flows: [],
      },
      runtimePhases: [
        {
          flowId: 'flow-debug',
          flowName: 'Debug Flow',
          nodeId: 'click-send',
          phase: 'waitingForSelector',
          message: 'Aguardando botao Enviar',
          detail: {
            selector: 'name=Enviar',
            fallback: 'Fallback visual bloqueado por cor divergente',
            nextStep: 'Repare o seletor com Mira e rode Dry-run novamente',
          },
          timestamp: '2026-05-12T12:00:02.000Z',
        },
      ],
      nodeStatusTimeline: [
        { at: '2026-05-12T12:00:02.000Z', nodeId: 'click-send', status: 'Running' },
      ],
      logs: [],
      isLogsExpanded: false,
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(ExecutionStatus));
    });

    const overlay = container.querySelector('.exec-status__debug-overlay');
    expect(overlay?.textContent).toContain('Node atual');
    expect(overlay?.textContent).toContain('click-send');
    expect(overlay?.textContent).toContain('waitingForSelector');
    expect(overlay?.textContent).toContain('name=Enviar');
    expect(overlay?.textContent).toContain('Fallback visual bloqueado');
    expect(overlay?.textContent).toContain('Repare o seletor com Mira');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);

  it('stays compact while editing and expands actionable cards only for runtime/debug context', async () => {
    const React = await import('react');
    const { useAppStore } = await import('../../store/appStore');
    const { useFlowStore } = await import('../../store/flowStore');
    const { default: ExecutionStatus } = await import('./ExecutionStatus');

    useFlowStore.setState({
      flowId: 'flow-idle',
      flowName: 'Idle Flow',
      validationResult: null,
    });
    useAppStore.setState({
      runtimeStatus: {
        isRunning: false,
        queueLength: 0,
        armedFlowCount: 0,
        currentRun: null,
        flows: [],
      },
      flowRuntimes: {},
      currentRun: null,
      logs: [],
      isLogsExpanded: false,
      debugVisualEnabled: false,
      flowHealthReport: null,
      runtimePhases: [],
    });

    const container = document.createElement('div');
    document.body.appendChild(container);
    const root = createRoot(container);

    await act(async () => {
      root.render(React.createElement(ExecutionStatus));
    });

    expect(container.querySelector('.exec-status__summary')).toBeNull();
    expect(container.textContent).toContain('Proxima acao');

    await act(async () => {
      useAppStore.setState({
        runtimeStatus: {
          isRunning: true,
          queueLength: 1,
          armedFlowCount: 0,
          currentRun: {
            flowId: 'flow-idle',
            flowName: 'Idle Flow',
            source: 'manual',
            startedAt: '2026-05-12T12:00:00.000Z',
          },
          flows: [],
        },
        currentRun: {
          flowId: 'flow-idle',
          flowName: 'Idle Flow',
          source: 'manual',
          startedAt: '2026-05-12T12:00:00.000Z',
        },
      });
    });

    await act(async () => {
      root.render(React.createElement(ExecutionStatus));
    });

    expect(container.querySelector('.exec-status__summary')).toBeTruthy();
    expect(container.textContent).toContain('Fila: 1');

    await act(async () => {
      root.unmount();
    });
  }, 15_000);
});
