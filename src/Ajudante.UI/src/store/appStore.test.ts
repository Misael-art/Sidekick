import { beforeEach, describe, expect, it, vi } from 'vitest';

describe('appStore runtime state', () => {
  beforeEach(async () => {
    vi.resetModules();
  });

  it('stores runtime snapshot fields separately from editor state', async () => {
    const { useAppStore } = await import('./appStore');

    useAppStore.getState().setRuntimeStatus({
      isRunning: true,
      queueLength: 3,
      armedFlowCount: 2,
      currentRun: {
        flowId: 'flow-1',
        flowName: 'Runtime Flow',
        source: 'manual',
        startedAt: '2026-04-24T12:00:00.000Z',
      },
      flows: [
        {
          flowId: 'flow-1',
          flowName: 'Runtime Flow',
          state: 'running',
          isArmed: true,
          isRunning: true,
          queuePending: true,
          activeTriggerNodeIds: ['trigger-1'],
        },
      ],
    });

    const state = useAppStore.getState();
    expect(state.isRunning).toBe(true);
    expect(state.queueLength).toBe(3);
    expect(state.armedFlowCount).toBe(2);
    expect(state.currentRun?.flowId).toBe('flow-1');
    expect(state.flowRuntimes['flow-1']?.isArmed).toBe(true);
    expect(state.runtimeStatus.flows).toHaveLength(1);
  });

  it('upserts and removes per-flow runtime entries', async () => {
    const { useAppStore } = await import('./appStore');

    useAppStore.getState().upsertFlowRuntime({
      flowId: 'flow-queued',
      flowName: 'Queued Flow',
      state: 'queued',
      isArmed: true,
      isRunning: false,
      queuePending: true,
      activeTriggerNodeIds: ['trigger-1'],
    });

    expect(useAppStore.getState().flowRuntimes['flow-queued']?.state).toBe('queued');
    expect(useAppStore.getState().armedFlowCount).toBe(1);

    useAppStore.getState().upsertFlowRuntime({
      flowId: 'flow-queued',
      flowName: 'Queued Flow',
      state: 'inactive',
      isArmed: false,
      isRunning: false,
      queuePending: false,
      activeTriggerNodeIds: [],
      lastError: undefined,
    });

    expect(useAppStore.getState().flowRuntimes['flow-queued']).toBeUndefined();
  });
});
