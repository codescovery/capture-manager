import { useEffect, useRef, useState, type RefObject } from 'react';
import { parseBridgeMessage, type BridgeFocus } from '../lib/focus';
import { VideoPlayer } from '../lib/videoPlayer';

export type StreamStatus = 'connecting' | 'connected' | 'offline';

export interface StreamStats {
  width: number;
  height: number;
  fps: number;
  kbps: number;
}

const DESKTOP_APP_URL = 'ws://127.0.0.1:47800';
const RECONNECT_DELAY_MS = 2000;
const STATS_INTERVAL_MS = 1000;
const EMPTY_STATS: StreamStats = { width: 0, height: 0, fps: 0, kbps: 0 };

/** Served by the desktop app: same host. Anywhere else (e.g. the Vite dev server): the default app port. */
function resolveBridgeUrl(): string {
  return location.port === '47800' ? `ws://${location.host}` : DESKTOP_APP_URL;
}

interface StreamState {
  status: StreamStatus;
  focus: BridgeFocus | null;
  stats: StreamStats;
  hasPicture: boolean;
}

/** Connects to the desktop app: focus messages (text) and the video of the focused window (binary H.264). */
export function useStream(canvasRef: RefObject<HTMLCanvasElement>, keepLast: boolean): StreamState {
  const [status, setStatus] = useState<StreamStatus>('connecting');
  const [focus, setFocus] = useState<BridgeFocus | null>(null);
  const [stats, setStats] = useState<StreamStats>(EMPTY_STATS);
  const [hasPicture, setHasPicture] = useState(false);
  const playerRef = useRef<VideoPlayer | null>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    let socket: WebSocket | null = null;
    let retry: number | undefined;
    let disposed = false;
    const requestKeyFrame = () => socket?.readyState === WebSocket.OPEN && socket.send('key');
    const player = new VideoPlayer(canvas, requestKeyFrame, () => setHasPicture(true));
    playerRef.current = player;

    const connect = () => {
      setStatus('connecting');
      socket = new WebSocket(resolveBridgeUrl());
      socket.binaryType = 'arraybuffer';
      socket.onopen = () => setStatus('connected');
      socket.onmessage = (event) => {
        if (typeof event.data === 'string') return handleText(event.data);
        player.push(event.data as ArrayBuffer);
      };
      socket.onclose = () => {
        if (disposed) return;
        setStatus('offline');
        setFocus(null);
        retry = window.setTimeout(connect, RECONNECT_DELAY_MS);
      };
    };

    const handleText = (raw: string) => {
      const next = parseBridgeMessage(raw);
      if (!next) return;
      player.setVisible(next.focused);
      setFocus(next);
    };

    const statsTimer = window.setInterval(() => setStats(readStats(player)), STATS_INTERVAL_MS);
    connect();
    return () => {
      disposed = true;
      window.clearTimeout(retry);
      window.clearInterval(statsTimer);
      socket?.close();
      player.close();
      playerRef.current = null;
    };
  }, [canvasRef]);

  useEffect(() => {
    if (focus?.focused !== false || keepLast) return;
    playerRef.current?.clear();
    setHasPicture(false);
  }, [focus, keepLast]);

  return { status, focus, stats, hasPicture };
}

function readStats(player: VideoPlayer): StreamStats {
  const { width, height } = player.counters;
  return { width, height, ...player.sampleRates() };
}
