import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { initBridge, sendCommand } from './bridge';
import type { BridgeMessage } from './types';

describe('bridge', () => {
  let messageHandler: ((event: MessageEvent<unknown>) => void) | undefined;
  let postMessage: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    messageHandler = undefined;
    postMessage = vi.fn();

    Object.defineProperty(window, 'chrome', {
      configurable: true,
      value: {
        webview: {
          postMessage,
          addEventListener: vi.fn((event: string, listener: (event: MessageEvent<unknown>) => void) => {
            if (event === 'message') {
              messageHandler = listener;
            }
          }),
        },
      },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    delete (window as Window & { chrome?: unknown }).chrome;
  });

  it('rejects pending commands when the host responds with an error', async () => {
    initBridge();

    const responsePromise = sendCommand('flow', 'newFlow', { name: 'Broken Flow' });
    const request = JSON.parse(postMessage.mock.calls[0][0]) as BridgeMessage;

    messageHandler?.({
      data: {
        type: 'response',
        channel: 'flow',
        action: 'newFlow',
        requestId: request.requestId,
        error: 'No flowId specified',
      },
    } as MessageEvent<unknown>);

    await expect(responsePromise).rejects.toThrow('No flowId specified');
  });
});
