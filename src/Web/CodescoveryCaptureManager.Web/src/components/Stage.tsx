import { useEffect, useRef } from 'react';
import { subscribeFrames } from '../lib/frameClock';

export type StageNotice = 'empty' | 'no-focus' | 'unmatched' | 'paused';

const NOTICES: Record<StageNotice, { title: string; text: string }> = {
  empty: {
    title: 'Add a window to start',
    text: 'Pick the windows you want to expose. Then share this tab (or use Output only) in Teams, Meet or OBS.',
  },
  'no-focus': {
    title: 'No selected window is focused',
    text: 'Focus one of your selected windows to show it here.',
  },
  unmatched: {
    title: 'Focused window is not added here',
    text: 'The window in focus is not one of the sources in this browser. Nothing is shown.',
  },
  paused: { title: 'Nothing selected', text: 'Choose a source to show.' },
};

interface StageProps {
  video: HTMLVideoElement | undefined;
  notice: StageNotice | null;
}

export function Stage({ video, notice }: StageProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    return subscribeFrames(() => drawFrame(canvas, video));
  }, [video]);

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

function drawFrame(canvas: HTMLCanvasElement, video: HTMLVideoElement | undefined): void {
  const context = canvas.getContext('2d');
  if (!context) return;
  if (!video || !video.videoWidth) {
    context.clearRect(0, 0, canvas.width, canvas.height);
    return;
  }
  if (canvas.width !== video.videoWidth || canvas.height !== video.videoHeight) {
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
  }
  context.drawImage(video, 0, 0, canvas.width, canvas.height);
}
