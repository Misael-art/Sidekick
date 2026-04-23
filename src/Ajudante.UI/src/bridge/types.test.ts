import { describe, expect, it } from 'vitest';
import { normalizeNodeStatus } from './types';

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
});
