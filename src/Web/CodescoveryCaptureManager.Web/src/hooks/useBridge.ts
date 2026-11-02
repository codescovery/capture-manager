import { useEffect, useState } from 'react';
import { parseBridgeMessage, type BridgeFocus } from '../lib/focus';

export type BridgeStatus = 'connecting' | 'connected' | 'offline';

const DESKTOP_APP_URL = 'ws://127.0.0.1:47800';
const RECONNECT_DELAY_MS = 2000;

/** Served by the desktop app: same host. Anywhere else (e.g. the Vite dev server): the default app port. */
function resolveBridgeUrl(): string {
  const servedByApp = location.port === '47800';
  return servedByApp ? `ws://${location.host}` : DESKTOP_APP_URL;
}

export function useBridge(): { status: BridgeStatus; focus: BridgeFocus | null } {
  const [status, setStatus] = useState<BridgeStatus>('connecting');
  const [focus, setFocus] = useState<BridgeFocus | null>(null);

  useEffect(() => {
    let socket: WebSocket | null = null;
    let retry: number | undefined;
    let disposed = false;

    const connect = () => {
      setStatus('connecting');
      socket = new WebSocket(resolveBridgeUrl());
      socket.onopen = () => setStatus('connected');
      socket.onmessage = (event) => {
        const next = parseBridgeMessage(String(event.data));
        if (next) setFocus(next);
      };
      socket.onclose = () => {
        if (disposed) return;
        setStatus('offline');
        setFocus(null);
        retry = window.setTimeout(connect, RECONNECT_DELAY_MS);
      };
    };

    connect();
    return () => {
      disposed = true;
      window.clearTimeout(retry);
      socket?.close();
    };
  }, []);

  return { status, focus };
}
