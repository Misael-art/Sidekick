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
});
