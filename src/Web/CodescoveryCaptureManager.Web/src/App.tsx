import { useCallback, useEffect, useMemo, useState } from 'react';
import { ControlPanel, type Fallback } from './components/ControlPanel';
import { Stage, type StageNotice } from './components/Stage';
import { floatingPanelSupported, useFloatingPanel } from './hooks/useFloatingPanel';
import { useBridge } from './hooks/useBridge';
import { useSources } from './hooks/useSources';
import { findSource } from './lib/focus';

const FALLBACK_KEY = 'capture-manager.fallback';

export function App() {
  const bridge = useBridge();
  const { sources, videoOf, add, remove, rename } = useSources();
  const [manualId, setManualId] = useState<number | null>(null);
  const [lastShownId, setLastShownId] = useState<number | null>(null);
  const [follow, setFollow] = useState(true);
  const [fallback, setFallback] = useFallbackSetting();
  const [outputOnly, setOutputOnly] = useState(false);
  const [status, setStatus] = useState('');

  const following = follow && bridge.status === 'connected';
  const view = useMemo(
    () => resolveView({ sources, manualId, following, focus: bridge.focus, fallback, lastShownId }),
    [sources, manualId, following, bridge.focus, fallback, lastShownId],
  );

  useEffect(() => {
    if (view.liveId !== null) setLastShownId(view.liveId);
  }, [view.liveId]);

  const select = useCallback((id: number) => {
    setManualId(id);
    setFollow(false);
  }, []);

  const handleKey = useCallback(
    (event: KeyboardEvent) => {
      if (event.key === 'Escape') return setOutputOnly(false);
      if (event.altKey || event.ctrlKey || event.metaKey) return;
      const target = sources[Number(event.key) - 1];
      if (target) select(target.id);
    },
    [sources, select],
  );

  useEffect(() => {
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [handleKey]);

  const floating = useFloatingPanel(handleKey);

  const addWindow = async () => {
    const failure = await add();
    setStatus(failure ?? '');
  };

  const panel = (
    <ControlPanel
      sources={sources}
      shownId={view.shownId}
      videoOf={videoOf}
      bridgeStatus={bridge.status}
      follow={follow}
      fallback={fallback}
      status={status}
      floating={floating.isOpen}
      canFloat={floatingPanelSupported}
      onAdd={addWindow}
      onSelect={select}
      onRemove={remove}
      onRename={rename}
      onToggleFollow={() => setFollow((current) => !current)}
      onFallback={setFallback}
      onFloat={floating.open}
      onOutputOnly={() => setOutputOnly(true)}
    />
  );

  return (
    <div className="app" data-output-only={outputOnly} data-floating={floating.isOpen}>
      {!outputOnly && <Header />}
      <main>
        <Stage video={view.shownId === null ? undefined : videoOf(view.shownId)} notice={view.notice} />
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
      <p className="muted">Share <b>this tab</b>. Use “Output only” so thumbnails of other windows never go on air.</p>
    </header>
  );
}

interface ViewInput {
  sources: ReturnType<typeof useSources>['sources'];
  manualId: number | null;
  following: boolean;
  focus: ReturnType<typeof useBridge>['focus'];
  fallback: Fallback;
  lastShownId: number | null;
}

interface ResolvedView {
  liveId: number | null;
  shownId: number | null;
  notice: StageNotice | null;
}

function resolveView({ sources, manualId, following, focus, fallback, lastShownId }: ViewInput): ResolvedView {
  if (sources.length === 0) return { liveId: null, shownId: null, notice: 'empty' };
  if (!following) return resolveManual(sources, manualId);
  if (focus?.focused) return resolveFocused(sources, focus);
  return resolveBlurred(sources, fallback, lastShownId);
}

function resolveManual(sources: ViewInput['sources'], manualId: number | null): ResolvedView {
  const id = sources.some((source) => source.id === manualId) ? manualId : null;
  return { liveId: id, shownId: id, notice: id === null ? 'paused' : null };
}

function resolveFocused(sources: ViewInput['sources'], focus: NonNullable<ViewInput['focus']>): ResolvedView {
  const match = findSource(sources, focus);
  return match ? { liveId: match.id, shownId: match.id, notice: null } : { liveId: null, shownId: null, notice: 'unmatched' };
}

function resolveBlurred(sources: ViewInput['sources'], fallback: Fallback, lastShownId: number | null): ResolvedView {
  const kept = fallback === 'keep' && sources.some((source) => source.id === lastShownId) ? lastShownId : null;
  return { liveId: null, shownId: kept, notice: kept === null ? 'no-focus' : null };
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
