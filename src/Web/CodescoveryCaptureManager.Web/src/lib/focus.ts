export interface BridgeFocus {
  focused: boolean;
  focusedTitle?: string;
  processName?: string;
  handle?: number;
}

export function parseBridgeMessage(raw: string): BridgeFocus | null {
  try {
    return toFocus(JSON.parse(raw));
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
