import { codecFromSps, findSps, sameBytes } from './annexb';

const MAX_QUEUE = 4;
const KEY_FRAME_FLAG = 1;
const HEADER_BYTES = 2;

export interface PlayerCounters {
  frames: number;
  bytes: number;
  width: number;
  height: number;
}

/**
 * Decodes the H.264 stream of the desktop app (Annex-B access units) with WebCodecs and paints each frame to a
 * canvas as soon as it is decoded: no buffering, no rAF, so the latency is the decoder's own.
 */
export class VideoPlayer {
  readonly counters: PlayerCounters = { frames: 0, bytes: 0, width: 0, height: 0 };

  private readonly context: CanvasRenderingContext2D | null;
  private decoder: VideoDecoder | null = null;
  private sps: Uint8Array | null = null;
  private waitingForKeyFrame = true;
  private visible = false;
  private sampledFrames = 0;
  private sampledBytes = 0;

  constructor(
    private readonly canvas: HTMLCanvasElement,
    private readonly requestKeyFrame: () => void,
    private readonly onPicture: () => void,
  ) {
    this.context = canvas.getContext('2d', { alpha: false, desynchronized: true });
  }

  static isSupported(): boolean {
    return typeof VideoDecoder !== 'undefined' && typeof EncodedVideoChunk !== 'undefined';
  }

  setVisible(visible: boolean): void {
    this.visible = visible;
  }

  clear(): void {
    this.context?.clearRect(0, 0, this.canvas.width, this.canvas.height);
  }

  push(message: ArrayBuffer): void {
    const bytes = new Uint8Array(message);
    if (bytes.length <= HEADER_BYTES) return;

    const isKeyFrame = (bytes[1] & KEY_FRAME_FLAG) !== 0;
    const accessUnit = bytes.subarray(HEADER_BYTES);
    this.counters.bytes += accessUnit.length;
    if (isKeyFrame && !this.configureFor(accessUnit)) return;
    this.decode(accessUnit, isKeyFrame);
  }

  /** Frames and kilobits received since the previous call (call once per second for fps / kbps). */
  sampleRates(): { fps: number; kbps: number } {
    const fps = this.counters.frames - this.sampledFrames;
    const kbps = Math.round(((this.counters.bytes - this.sampledBytes) * 8) / 1000);
    this.sampledFrames = this.counters.frames;
    this.sampledBytes = this.counters.bytes;
    return { fps, kbps };
  }

  close(): void {
    if (this.decoder && this.decoder.state !== 'closed') this.decoder.close();
    this.decoder = null;
  }

  private configureFor(accessUnit: Uint8Array): boolean {
    const sps = findSps(accessUnit);
    if (!sps) return this.decoder !== null;
    if (this.decoder && sameBytes(sps, this.sps)) return true;

    const codec = codecFromSps(sps);
    if (!codec) return false;
    this.sps = sps.slice();
    this.resetDecoder(codec);
    return true;
  }

  private resetDecoder(codec: string): void {
    this.close();
    const decoder = new VideoDecoder({
      output: (frame) => this.paint(frame),
      error: () => this.recover(),
    });
    decoder.configure({
      codec,
      optimizeForLatency: true,
      hardwareAcceleration: 'prefer-hardware',
      avc: { format: 'annexb' },
    } as VideoDecoderConfig);
    this.decoder = decoder;
    this.waitingForKeyFrame = true;
  }

  private decode(accessUnit: Uint8Array, isKeyFrame: boolean): void {
    const decoder = this.decoder;
    if (!decoder || decoder.state !== 'configured') return;
    if (isKeyFrame) this.waitingForKeyFrame = false;
    if (this.waitingForKeyFrame) return;

    if (!isKeyFrame && decoder.decodeQueueSize > MAX_QUEUE) {
      this.waitingForKeyFrame = true;
      this.requestKeyFrame();
      return;
    }

    decoder.decode(
      new EncodedVideoChunk({ type: isKeyFrame ? 'key' : 'delta', timestamp: performance.now() * 1000, data: accessUnit }),
    );
  }

  private recover(): void {
    this.decoder = null;
    this.sps = null;
    this.waitingForKeyFrame = true;
    this.requestKeyFrame();
  }

  private paint(frame: VideoFrame): void {
    try {
      if (!this.visible || !this.context) return;
      this.fitCanvas(frame.displayWidth, frame.displayHeight);
      this.context.drawImage(frame, 0, 0);
      this.counters.frames++;
      this.onPicture();
    } finally {
      frame.close();
    }
  }

  private fitCanvas(width: number, height: number): void {
    if (this.canvas.width === width && this.canvas.height === height) return;
    this.canvas.width = width;
    this.canvas.height = height;
    this.counters.width = width;
    this.counters.height = height;
  }
}
