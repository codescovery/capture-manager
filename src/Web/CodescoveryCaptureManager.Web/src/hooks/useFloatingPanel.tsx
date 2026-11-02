import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';

interface DocumentPictureInPicture {
  requestWindow(options: { width: number; height: number }): Promise<Window>;
}

declare global {
  interface Window {
    documentPictureInPicture?: DocumentPictureInPicture;
  }
}

export const floatingPanelSupported = typeof window !== 'undefined' && 'documentPictureInPicture' in window;

/** Always-on-top mini window (Document Picture-in-Picture, Chrome/Edge 116+) that hosts React children. */
export function useFloatingPanel(onKeyDown: (event: KeyboardEvent) => void) {
  const [panel, setPanel] = useState<Window | null>(null);
  const keyHandler = useRef(onKeyDown);
  keyHandler.current = onKeyDown;

  const open = useCallback(async () => {
    if (panel || !window.documentPictureInPicture) return;
    const floating = await window.documentPictureInPicture.requestWindow({ width: 300, height: 360 });
    copyStyles(floating);
    floating.addEventListener('keydown', (event) => keyHandler.current(event));
    floating.addEventListener('pagehide', () => setPanel(null));
    setPanel(floating);
  }, [panel]);

  useEffect(() => () => panel?.close(), [panel]);

  const render = (children: ReactNode) => (panel ? createPortal(children, panel.document.body) : null);

  return { isOpen: panel !== null, open, render };
}

function copyStyles(target: Window): void {
  document.querySelectorAll('link[rel="stylesheet"], style').forEach((node) => {
    target.document.head.append(node.cloneNode(true));
  });
  target.document.documentElement.dataset.floating = 'true';
}
