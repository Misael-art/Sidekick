import type { BridgeMessage } from './types';

type EventCallback = (payload: any) => void;

interface PendingRequest {
  resolve: (value: any) => void;
  reject: (reason: any) => void;
  timer: ReturnType<typeof setTimeout>;
}

const pendingRequests = new Map<string, PendingRequest>();
const eventListeners = new Map<string, Set<EventCallback>>();

const REQUEST_TIMEOUT_MS = 30_000;

let requestCounter = 0;

function generateRequestId(): string {
  return `req_${Date.now()}_${++requestCounter}`;
}

function listenerKey(channel: string, action: string): string {
  return `${channel}:${action}`;
}

/** True when running inside WebView2 with the host bridge available. */
function isWebView2(): boolean {
  return (
    typeof window !== 'undefined' &&
    'chrome' in window &&
    !!(window as any).chrome?.webview?.postMessage
  );
}

/**
 * Send a command to the C# host and return a promise that resolves
 * with the response payload.
 */
export function sendCommand(
  channel: BridgeMessage['channel'],
  action: string,
  payload: any = {},
): Promise<any> {
  const requestId = generateRequestId();

  const message: BridgeMessage = {
    type: 'command',
    channel,
    action,
    requestId,
    payload,
  };

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      pendingRequests.delete(requestId);
      reject(new Error(`Bridge request timed out: ${channel}/${action}`));
    }, REQUEST_TIMEOUT_MS);

    pendingRequests.set(requestId, { resolve, reject, timer });

    if (isWebView2()) {
      (window as any).chrome.webview.postMessage(JSON.stringify(message));
    } else {
      // Dev-mode fallback: log and auto-resolve with empty payload
      console.log('[Bridge] sendCommand (dev):', message);
      clearTimeout(timer);
      pendingRequests.delete(requestId);
      resolve({});
    }
  });
}

/**
 * Register a listener for events pushed from the C# host.
 * Returns an unsubscribe function.
 */
export function onEvent(
  channel: BridgeMessage['channel'],
  action: string,
  callback: EventCallback,
): () => void {
  const key = listenerKey(channel, action);
  if (!eventListeners.has(key)) {
    eventListeners.set(key, new Set());
  }
  eventListeners.get(key)!.add(callback);

  return () => {
    eventListeners.get(key)?.delete(callback);
  };
}

/**
 * Called by the global message handler to dispatch an incoming
 * message from C#.
 */
function handleIncomingMessage(msg: BridgeMessage): void {
  if (msg.type === 'response') {
    const pending = pendingRequests.get(msg.requestId);
    if (pending) {
      clearTimeout(pending.timer);
      pendingRequests.delete(msg.requestId);
      pending.resolve(msg.payload);
    }
    return;
  }

  if (msg.type === 'event') {
    const key = listenerKey(msg.channel, msg.action);
    const listeners = eventListeners.get(key);
    if (listeners) {
      for (const cb of listeners) {
        try {
          cb(msg.payload);
        } catch (err) {
          console.error('[Bridge] Event listener error:', err);
        }
      }
    }
  }
}

/** Bootstrap the bridge — call once at app startup. */
export function initBridge(): void {
  if (isWebView2()) {
    (window as any).chrome.webview.addEventListener(
      'message',
      (event: MessageEvent) => {
        try {
          const msg: BridgeMessage =
            typeof event.data === 'string'
              ? JSON.parse(event.data)
              : event.data;
          handleIncomingMessage(msg);
        } catch (err) {
          console.error('[Bridge] Failed to parse incoming message:', err);
        }
      },
    );
    console.log('[Bridge] WebView2 bridge initialised.');
  } else {
    console.log('[Bridge] Running in dev mode — no WebView2 host.');
  }
}
