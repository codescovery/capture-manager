import type { RefObject } from 'react';

export type StageNotice = 'connecting' | 'offline' | 'no-focus' | 'waiting' | 'unsupported';

const NOTICES: Record<StageNotice, { title: string; text: string }> = {
  connecting: { title: 'Connecting to the desktop app…', text: 'Looking for Capture Manager on this computer.' },
  offline: {
    title: 'Capture Manager is not running',
    text: 'Start the desktop app, then pick the windows you want to expose. This page reconnects by itself.',
  },
  'no-focus': {
    title: 'No selected window is focused',
    text: 'Focus one of the windows you picked in Capture Manager to show it here.',
  },
  waiting: { title: 'Waiting for video…', text: 'The focused window is about to appear.' },
  unsupported: {
    title: 'This browser cannot decode the stream',
    text: 'Use a recent Chrome or Edge on desktop (WebCodecs is required).',
  },
};

interface StageProps {
  canvasRef: RefObject<HTMLCanvasElement>;
  notice: StageNotice | null;
}

export function Stage({ canvasRef, notice }: StageProps) {
  return (
    <div className="stage" data-blank={notice !== null}>
      <canvas ref={canvasRef} width={1280} height={720} />
      {notice && <StageMessage notice={notice} />}
    </div>
  );
}

function StageMessage({ notice }: { notice: StageNotice }) {
  const { title, text } = NOTICES[notice];
  return (
    <div className="stage-message" role="status">
      <img src="./icon.svg" alt="" width={56} height={56} />
      <h2>{title}</h2>
      <p>{text}</p>
    </div>
  );
}
