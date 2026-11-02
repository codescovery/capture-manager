import { useCallback, useEffect, useRef, useState } from 'react';

export interface Source {
  id: number;
  label: string;
  name: string;
}

export interface SourcesApi {
  sources: Source[];
  videoOf: (id: number) => HTMLVideoElement | undefined;
  add: () => Promise<string | null>;
  remove: (id: number) => void;
  rename: (id: number, name: string) => void;
}

const CAPTURE_UNSUPPORTED = 'This browser has no screen capture support. Use Chrome or Edge on desktop.';

export function useSources(): SourcesApi {
  const [sources, setSources] = useState<Source[]>([]);
  const streams = useRef(new Map<number, { stream: MediaStream; video: HTMLVideoElement }>());
  const nextId = useRef(1);

  const remove = useCallback((id: number) => {
    const entry = streams.current.get(id);
    if (!entry) return;
    entry.stream.getTracks().forEach((track) => track.stop());
    entry.video.srcObject = null;
    streams.current.delete(id);
    setSources((current) => current.filter((source) => source.id !== id));
  }, []);

  const register = useCallback(
    (stream: MediaStream) => {
      const [track] = stream.getVideoTracks();
      const id = nextId.current++;
      const video = document.createElement('video');
      video.muted = true;
      video.playsInline = true;
      video.srcObject = stream;
      video.play().catch(() => {});
      streams.current.set(id, { stream, video });
      track.addEventListener('ended', () => remove(id));
      const label = track.label || `window:${id}`;
      setSources((current) => [...current, { id, label, name: defaultName(label, id) }]);
    },
    [remove],
  );

  const add = useCallback(async () => {
    if (!navigator.mediaDevices?.getDisplayMedia) return CAPTURE_UNSUPPORTED;
    try {
      const stream = await navigator.mediaDevices.getDisplayMedia({
        video: { frameRate: 30 },
        audio: false,
        selfBrowserSurface: 'exclude',
        surfaceSwitching: 'exclude',
      } as DisplayMediaStreamOptions);
      register(stream);
      return null;
    } catch (error) {
      return describeCaptureError(error);
    }
  }, [register]);

  const rename = useCallback((id: number, name: string) => {
    setSources((current) => current.map((source) => (source.id === id ? { ...source, name } : source)));
  }, []);

  useEffect(() => {
    const active = streams.current;
    return () => {
      active.forEach(({ stream }) => stream.getTracks().forEach((track) => track.stop()));
      active.clear();
    };
  }, []);

  const videoOf = useCallback((id: number) => streams.current.get(id)?.video, []);

  return { sources, videoOf, add, remove, rename };
}

// Chrome's window labels are `window:<id>:<n>`: useless to read, so start with a friendly number.
function defaultName(label: string, id: number): string {
  return /^(window|screen|web-contents-media-stream):/.test(label) ? `Window ${id}` : label;
}

function describeCaptureError(error: unknown): string | null {
  if (error instanceof DOMException && error.name === 'NotAllowedError') return null;
  return `Capture failed: ${error instanceof Error ? error.message : String(error)}`;
}
