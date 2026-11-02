import { useEffect, useRef, useState } from 'react';
import { ControlPanel, type Fallback } from './components/ControlPanel';
import { Stage, type StageNotice } from './components/Stage';
import { floatingPanelSupported, useFloatingPanel } from './hooks/useFloatingPanel';
import { useStream, type StreamStatus } from './hooks/useStream';
import type { BridgeFocus } from './lib/focus';
import { VideoPlayer } from './lib/videoPlayer';

const FALLBACK_KEY = 'capture-manager.fallback';

export function App() {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [fallback, setFallback] = useFallbackSetting();
  const [outputOnly, setOutputOnly] = useState(false);
  const stream = useStream(canvasRef, fallback === 'keep');
  const notice = resolveNotice(stream.status, stream.focus, stream.hasPicture, fallback);

  useEffect(() => {
    const leave = (event: KeyboardEvent) => event.key === 'Escape' && setOutputOnly(false);
    document.addEventListener('keydown', leave);
    return () => document.removeEventListener('keydown', leave);
  }, []);

  const floating = useFloatingPanel((event) => event.key === 'Escape' && setOutputOnly(false));

  const panel = (
    <ControlPanel
      status={stream.status}
      focusedTitle={stream.focus?.focused ? stream.focus.focusedTitle : undefined}
      stats={stream.stats}
      fallback={fallback}
      floating={floating.isOpen}
      canFloat={floatingPanelSupported}
      onFallback={setFallback}
      onFloat={floating.open}
      onOutputOnly={() => setOutputOnly(true)}
    />
  );

  return (
    <div className="app" data-output-only={outputOnly} data-floating={floating.isOpen}>
      {!outputOnly && <Header />}
      <main>
        <Stage canvasRef={canvasRef} notice={notice} />
        {!outputOnly && !floating.isOpen && <aside>{panel}</aside>}
      </main>
      {outputOnly && (
        <button type="button" className="exit-output" onClick={() => setOutputOnly(false)}>Exit output only (Esc)</button>
      )}
      {floating.render(panel)}
    </div>
  );
}

function Header() {
  return (
    <header>
      <img src="./icon.svg" alt="" width={28} height={28} />
      <h1>Capture Manager <span>web</span></h1>
      <p className="muted">Mirrors the desktop app: share <b>this tab</b>. Only the focused selected window is ever sent here.</p>
    </header>
  );
}

function resolveNotice(status: StreamStatus, focus: BridgeFocus | null, hasPicture: boolean, fallback: Fallback): StageNotice | null {
  if (!VideoPlayer.isSupported()) return 'unsupported';
  if (status === 'offline') return 'offline';
  if (status === 'connecting') return 'connecting';
  if (!focus || !focus.focused) return fallback === 'keep' && hasPicture ? null : 'no-focus';
  return hasPicture ? null : 'waiting';
}

function useFallbackSetting(): [Fallback, (value: Fallback) => void] {
  const [fallback, setFallback] = useState<Fallback>(readFallback);
  const update = (value: Fallback) => {
    setFallback(value);
    try {
      localStorage.setItem(FALLBACK_KEY, value);
    } catch {
      /* storage can be blocked; the choice then lasts for this session only */
    }
  };
  return [fallback, update];
}

function readFallback(): Fallback {
  try {
    return localStorage.getItem(FALLBACK_KEY) === 'keep' ? 'keep' : 'message';
  } catch {
    return 'message';
  }
}
