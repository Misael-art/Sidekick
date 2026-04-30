import { describe, expect, it } from 'vitest';
import {
  normalizeNodeStatus,
  normalizeRuntimeStatusSnapshot,
} from './types';

describe('bridge types helpers', () => {
  it('normalizes lowercase status values from the backend', () => {
    expect(normalizeNodeStatus('running')).toBe('Running');
    expect(normalizeNodeStatus('completed')).toBe('Completed');
    expect(normalizeNodeStatus('error')).toBe('Error');
  });

  it('preserves already normalized values and falls back safely', () => {
    expect(normalizeNodeStatus('Idle')).toBe('Idle');
    expect(normalizeNodeStatus('Skipped')).toBe('Skipped');
    expect(normalizeNodeStatus('unknown')).toBe('Idle');
    expect(normalizeNodeStatus(undefined)).toBe('Idle');
  });

  it('normalizes runtime snapshots from backend events', () => {
    const snapshot = normalizeRuntimeStatusSnapshot({
      isRunning: true,
      queueLength: 2,
      armedFlowCount: 1,
      currentRun: {
        flowId: 'flow-1',
        flowName: 'Runtime Flow',
        source: 'trigger',
        triggerNodeId: 'trigger-1',
        startedAt: '2026-04-24T12:00:00.000Z',
      },
      flows: [
        {
          flowId: 'flow-1',
          flowName: 'Runtime Flow',
          state: 'queued',
          isArmed: true,
          isRunning: false,
          queuePending: true,
          activeTriggerNodeIds: ['trigger-1'],
          lastTriggerAt: '2026-04-24T11:59:00.000Z',
        },
      ],
    });

    expect(snapshot.isRunning).toBe(true);
    expect(snapshot.queueLength).toBe(2);
    expect(snapshot.currentRun?.source).toBe('trigger');
    expect(snapshot.flows[0].state).toBe('queued');
    expect(snapshot.flows[0].activeTriggerNodeIds).toEqual(['trigger-1']);
  });
});
