import { useEffect, useRef, useState } from 'react';
import { subscribeFrames } from '../lib/frameClock';
import type { Source } from '../hooks/useSources';

const THUMB_WIDTH = 112;
const THUMB_HEIGHT = 63;

interface SourceListProps {
  sources: Source[];
  shownId: number | null;
  videoOf: (id: number) => HTMLVideoElement | undefined;
  onSelect: (id: number) => void;
  onRemove: (id: number) => void;
  onRename: (id: number, name: string) => void;
}

export function SourceList({ sources, shownId, videoOf, onSelect, onRemove, onRename }: SourceListProps) {
  if (sources.length === 0) return <p className="muted">No windows yet.</p>;

  return (
    <ul className="sources">
      {sources.map((source, index) => (
        <SourceItem
          key={source.id}
          source={source}
          shortcut={index + 1}
          current={source.id === shownId}
          video={videoOf(source.id)}
          onSelect={() => onSelect(source.id)}
          onRemove={() => onRemove(source.id)}
          onRename={(name) => onRename(source.id, name)}
        />
      ))}
    </ul>
  );
}

interface SourceItemProps {
  source: Source;
  shortcut: number;
  current: boolean;
  video: HTMLVideoElement | undefined;
  onSelect: () => void;
  onRemove: () => void;
  onRename: (name: string) => void;
}

function SourceItem({ source, shortcut, current, video, onSelect, onRemove, onRename }: SourceItemProps) {
  const [editing, setEditing] = useState(false);

  return (
    <li className="source" aria-current={current} onClick={onSelect}>
      <Thumbnail video={video} />
      <div className="source-body">
        {editing ? (
          <NameEditor name={source.name} onDone={(name) => { onRename(name); setEditing(false); }} />
        ) : (
          <button type="button" className="source-name" title={`${source.name} (double-click to rename)`} onDoubleClick={() => setEditing(true)}>
            {source.name}
          </button>
        )}
        <span className="muted">Key {shortcut}</span>
      </div>
      <button
        type="button"
        className="icon-button"
        aria-label={`Remove ${source.name}`}
        onClick={(event) => { event.stopPropagation(); onRemove(); }}
      >
        ✕
      </button>
    </li>
  );
}

function NameEditor({ name, onDone }: { name: string; onDone: (name: string) => void }) {
  const finish = (value: string) => onDone(value.trim() || name);
  return (
    <input
      className="name-input"
      autoFocus
      defaultValue={name}
      onClick={(event) => event.stopPropagation()}
      onBlur={(event) => finish(event.currentTarget.value)}
      onKeyDown={(event) => {
        if (event.key === 'Enter') finish(event.currentTarget.value);
        if (event.key === 'Escape') onDone(name);
        event.stopPropagation();
      }}
    />
  );
}

function Thumbnail({ video }: { video: HTMLVideoElement | undefined }) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas || !video) return;
    return subscribeFrames(() => {
      if (!video.videoWidth) return;
      canvas.getContext('2d')?.drawImage(video, 0, 0, THUMB_WIDTH, THUMB_HEIGHT);
    });
  }, [video]);

  return <canvas ref={canvasRef} className="thumb" width={THUMB_WIDTH} height={THUMB_HEIGHT} />;
}
