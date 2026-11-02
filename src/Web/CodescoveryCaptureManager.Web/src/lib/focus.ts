export interface BridgeFocus {
  focused: boolean;
  focusedTitle?: string;
  processName?: string;
  handle?: number;
}

export interface MatchableSource {
  id: number;
  label: string;
  name: string;
}

const MIN_TITLE_MATCH_LENGTH = 3;

export function parseBridgeMessage(raw: string): BridgeFocus | null {
  try {
    const message = JSON.parse(raw);
    return toFocus(message);
  } catch {
    return null;
  }
}

function toFocus(message: unknown): BridgeFocus | null {
  if (typeof message !== 'object' || message === null) return null;
  const { focused, focusedTitle, processName, handle } = message as Record<string, unknown>;
  if (focused === false) return { focused: false };
  if (typeof focusedTitle !== 'string') return null;
  return {
    focused: true,
    focusedTitle,
    processName: typeof processName === 'string' ? processName : undefined,
    handle: typeof handle === 'number' ? handle : undefined,
  };
}

/** Chrome labels window tracks `window:<native id>:<n>`; on Windows the id is the HWND. */
export function handleFromLabel(label: string): number | null {
  const match = /^window:(\d+):/.exec(label);
  return match ? Number(match[1]) : null;
}

export function findSource<T extends MatchableSource>(sources: readonly T[], focus: BridgeFocus): T | undefined {
  if (!focus.focused) return undefined;
  return findByHandle(sources, focus.handle) ?? findByTitle(sources, focus.focusedTitle);
}

function findByHandle<T extends MatchableSource>(sources: readonly T[], handle?: number): T | undefined {
  if (handle === undefined) return undefined;
  return sources.find((source) => handleFromLabel(source.label) === handle);
}

function findByTitle<T extends MatchableSource>(sources: readonly T[], title?: string): T | undefined {
  if (!title) return undefined;
  const wanted = title.toLowerCase();
  return sources.find((source) => titlesOverlap(wanted, source.name) || titlesOverlap(wanted, source.label));
}

function titlesOverlap(wanted: string, candidate: string): boolean {
  const known = candidate.trim().toLowerCase();
  if (known.length < MIN_TITLE_MATCH_LENGTH) return false;
  return wanted.includes(known) || known.includes(wanted);
}
