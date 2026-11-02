import { BridgeBadge } from './BridgeBadge';
import { SourceList } from './SourceList';
import type { BridgeStatus } from '../hooks/useBridge';
import type { Source } from '../hooks/useSources';

export type Fallback = 'message' | 'keep';

interface ControlPanelProps {
  sources: Source[];
  shownId: number | null;
  videoOf: (id: number) => HTMLVideoElement | undefined;
  bridgeStatus: BridgeStatus;
  follow: boolean;
  fallback: Fallback;
  status: string;
  floating: boolean;
  canFloat: boolean;
  onAdd: () => void;
  onSelect: (id: number) => void;
  onRemove: (id: number) => void;
  onRename: (id: number, name: string) => void;
  onToggleFollow: () => void;
  onFallback: (fallback: Fallback) => void;
  onFloat: () => void;
  onOutputOnly: () => void;
}

export function ControlPanel(props: ControlPanelProps) {
  const { bridgeStatus, follow, fallback, status } = props;
  const canFollow = bridgeStatus === 'connected';

  return (
    <div className="panel">
      <div className="panel-actions">
        <button type="button" className="primary" onClick={props.onAdd}>+ Add window</button>
        <button type="button" onClick={props.onOutputOnly} title="Hide everything but the output (Esc to leave)">Output only</button>
        {props.canFloat && !props.floating && (
          <button type="button" onClick={props.onFloat} title="Keep these controls in a small always-on-top window">Float controls</button>
        )}
      </div>

      <section>
        <h2>Sources</h2>
        <SourceList
          sources={props.sources}
          shownId={props.shownId}
          videoOf={props.videoOf}
          onSelect={props.onSelect}
          onRemove={props.onRemove}
          onRename={props.onRename}
        />
      </section>

      <section>
        <h2>Focus</h2>
        <BridgeBadge status={bridgeStatus} />
        <label className="check">
          <input type="checkbox" checked={follow && canFollow} disabled={!canFollow} onChange={props.onToggleFollow} />
          Follow the focused window
        </label>
        <fieldset disabled={!canFollow}>
          <legend>When no selected window is focused</legend>
          <label className="check">
            <input type="radio" name="fallback" checked={fallback === 'message'} onChange={() => props.onFallback('message')} />
            Show a message
          </label>
          <label className="check">
            <input type="radio" name="fallback" checked={fallback === 'keep'} onChange={() => props.onFallback('keep')} />
            Keep the last window
          </label>
        </fieldset>
      </section>

      <p className="muted" role="status">{status || 'Tip: keys 1-9 switch sources.'}</p>
    </div>
  );
}
