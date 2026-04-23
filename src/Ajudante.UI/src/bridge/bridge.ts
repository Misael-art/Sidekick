import type { BridgeMessage } from './types';

type EventCallback = (payload: unknown) => void;

interface WebViewHost {
  postMessage(message: string): void;
  addEventListener(event: 'message', listener: (event: MessageEvent<unknown>) => void): void;
}

interface WebViewWindow extends Window {
  chrome?: {
    webview?: WebViewHost;
  };
}

interface PendingRequest {
  resolve: (value: unknown) => void;
  reject: (reason?: unknown) => void;
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

function getWindow(): WebViewWindow | undefined {
  if (typeof window === 'undefined') {
    return undefined;
  }

  return window as WebViewWindow;
}

/** True when running inside WebView2 with the host bridge available. */
function isWebView2(): boolean {
  return !!getWindow()?.chrome?.webview?.postMessage;
}

/**
 * Send a command to the C# host and return a promise that resolves
 * with the response payload.
 */
export function sendCommand<TResponse = unknown>(
  channel: BridgeMessage['channel'],
  action: string,
  payload: unknown = {},
): Promise<TResponse> {
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

    pendingRequests.set(requestId, {
      resolve: (value) => resolve(value as TResponse),
      reject,
      timer,
    });

    if (isWebView2()) {
      getWindow()!.chrome!.webview!.postMessage(JSON.stringify(message));
    } else {
      // Dev-mode fallback: log and auto-resolve with empty payload
      console.log('[Bridge] sendCommand (dev):', message);
      clearTimeout(timer);
      pendingRequests.delete(requestId);
      resolve({} as TResponse);
    }
  });
}

/**
 * Register a listener for events pushed from the C# host.
 * Returns an unsubscribe function.
 */
export function onEvent<TPayload = unknown>(
  channel: BridgeMessage['channel'],
  action: string,
  callback: (payload: TPayload) => void,
): () => void {
  const typedCallback = callback as EventCallback;
  const key = listenerKey(channel, action);
  if (!eventListeners.has(key)) {
    eventListeners.set(key, new Set());
  }
  eventListeners.get(key)!.add(typedCallback);

  return () => {
    eventListeners.get(key)?.delete(typedCallback);
  };
}

/**
 * Called by the global message handler to dispatch an incoming
 * message from C#.
 */
function handleIncomingMessage(msg: BridgeMessage): void {
  if (msg.type === 'response') {
    if (!msg.requestId) {
      return;
    }

    const pending = pendingRequests.get(msg.requestId);
    if (pending) {
      clearTimeout(pending.timer);
      pendingRequests.delete(msg.requestId);
      if (msg.error) {
        pending.reject(new Error(msg.error));
      } else {
        pending.resolve(msg.payload);
      }
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
    getWindow()!.chrome!.webview!.addEventListener(
      'message',
      (event: MessageEvent<unknown>) => {
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
