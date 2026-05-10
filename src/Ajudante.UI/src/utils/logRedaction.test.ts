import { describe, expect, it } from 'vitest';
import { redactLogMessage } from './logRedaction';

describe('redactLogMessage', () => {
  it('masks common secret patterns without hiding normal context', () => {
    const result = redactLogMessage('Executando token=abc123 password: senha Authorization: Bearer xyz sk-proj-live-key no node 4');

    expect(result).toContain('Executando');
    expect(result).toContain('node 4');
    expect(result).toContain('token=***');
    expect(result).toContain('password: ***');
    expect(result).not.toContain('abc123');
    expect(result).not.toContain('senha');
    expect(result).not.toContain('Bearer xyz');
    expect(result).not.toContain('sk-proj-live-key');
  });
});
