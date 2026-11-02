import type { StreamStats, StreamStatus } from '../hooks/useStream';

export type Fallback = 'message' | 'keep';

const STATUS_LABELS: Record<StreamStatus, string> = {
  connected: 'Streaming from the desktop app',
  connecting: 'Connecting to the desktop app…',
  offline: 'Desktop app not found',
};

interface ControlPanelProps {
  status: StreamStatus;
  focusedTitle: string | undefined;
  stats: StreamStats;
  fallback: Fallback;
  floating: boolean;
  canFloat: boolean;
  onFallback: (fallback: Fallback) => void;
  onFloat: () => void;
  onOutputOnly: () => void;
}

export function ControlPanel(props: ControlPanelProps) {
  const { status, stats } = props;

  return (
    <div className="panel">
      <div className="panel-actions">
        <button type="button" className="primary" onClick={props.onOutputOnly} title="Hide everything but the video (Esc to leave)">
          Output only
        </button>
        {props.canFloat && !props.floating && (
          <button type="button" onClick={props.onFloat} title="Keep these controls in a small always-on-top window">
            Float controls
          </button>
        )}
      </div>

      <section>
        <h2>Connection</h2>
        <span className="badge" data-status={status}>
          <i aria-hidden="true" />
          {STATUS_LABELS[status]}
        </span>
        <dl className="stats">
          <dt>Window</dt>
          <dd title={props.focusedTitle}>{props.focusedTitle ?? '—'}</dd>
          <dt>Video</dt>
          <dd>{stats.width ? `${stats.width}×${stats.height} · ${stats.fps} fps · ${(stats.kbps / 1000).toFixed(1)} Mbps` : '—'}</dd>
        </dl>
      </section>

      <section>
        <h2>Focus</h2>
        <fieldset>
          <legend>When no selected window is focused</legend>
          <label className="check">
            <input type="radio" name="fallback" checked={props.fallback === 'message'} onChange={() => props.onFallback('message')} />
            Show a message
          </label>
          <label className="check">
            <input type="radio" name="fallback" checked={props.fallback === 'keep'} onChange={() => props.onFallback('keep')} />
            Keep the last window
          </label>
        </fieldset>
        <p className="muted">Which windows are shared is chosen in the Capture Manager desktop app.</p>
      </section>
    </div>
  );
}
