type Listener = () => void;

const FRAME_INTERVAL_MS = 33;

const listeners = new Set<Listener>();
let worker: Worker | null = null;

// The timer lives in a worker: a covered or background tab would throttle setInterval/rAF to ~1 fps.
function ensureWorker(): void {
  if (worker) return;
  const code = `setInterval(() => postMessage(0), ${FRAME_INTERVAL_MS});`;
  worker = new Worker(URL.createObjectURL(new Blob([code], { type: 'text/javascript' })));
  worker.onmessage = () => listeners.forEach((listener) => listener());
}

export function subscribeFrames(listener: Listener): () => void {
  ensureWorker();
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
