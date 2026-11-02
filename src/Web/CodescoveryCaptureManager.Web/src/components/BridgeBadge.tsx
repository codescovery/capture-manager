import type { BridgeStatus } from '../hooks/useBridge';

const LABELS: Record<BridgeStatus, string> = {
  connected: 'Following desktop focus',
  connecting: 'Connecting to desktop app…',
  offline: 'Desktop app not found · manual mode',
};

export function BridgeBadge({ status }: { status: BridgeStatus }) {
  return (
    <span className="badge" data-status={status} title="Link to the Capture Manager desktop app (ws://127.0.0.1:47800)">
      <i aria-hidden="true" />
      {LABELS[status]}
    </span>
  );
}
