const output = document.getElementById('output');
const outputContext = output.getContext('2d');
const sourceList = document.getElementById('sources');
const emptyHint = document.getElementById('empty');
const statusLine = document.getElementById('status');
const addButton = document.getElementById('add');
const floatButton = document.getElementById('float');
const bridgeButton = document.getElementById('bridge');

const THUMBNAIL_WIDTH = 96;
const THUMBNAIL_HEIGHT = 54;
const FRAME_INTERVAL_MS = 33;

/** @type {{id:number, stream:MediaStream, video:HTMLVideoElement, label:string, item:HTMLLIElement, thumb:HTMLCanvasElement}[]} */
const sources = [];
let activeId = null;
let nextId = 1;
let bridgeSocket = null;
let floatingWindow = null;

function setStatus(message) {
  statusLine.textContent = message;
}

function activeSource() {
  return sources.find((source) => source.id === activeId) ?? null;
}

async function addWindow() {
  if (!navigator.mediaDevices?.getDisplayMedia) {
    setStatus('This browser has no screen capture support. Use Chrome or Edge on desktop.');
    return;
  }
  try {
    const stream = await navigator.mediaDevices.getDisplayMedia({
      video: { frameRate: 30 },
      audio: false,
      selfBrowserSurface: 'exclude',
      surfaceSwitching: 'exclude',
    });
    registerSource(stream);
  } catch (error) {
    setStatus(error.name === 'NotAllowedError' ? 'Selection cancelled.' : `Capture failed: ${error.message}`);
  }
}

function registerSource(stream) {
  const [track] = stream.getVideoTracks();
  const video = document.createElement('video');
  video.muted = true;
  video.playsInline = true;
  video.srcObject = stream;
  video.play().catch(() => {});

  const source = {
    id: nextId++,
    stream,
    video,
    label: track.label || `Window ${nextId - 1}`,
    item: document.createElement('li'),
    thumb: document.createElement('canvas'),
  };
  buildSourceItem(source);
  sources.push(source);
  track.addEventListener('ended', () => removeSource(source.id));

  if (activeId === null) select(source.id);
  renderState();
}

function buildSourceItem(source) {
  source.thumb.width = THUMBNAIL_WIDTH;
  source.thumb.height = THUMBNAIL_HEIGHT;

  const name = document.createElement('span');
  name.className = 'name';
  name.textContent = source.label;
  name.title = source.label;

  const remove = document.createElement('button');
  remove.type = 'button';
  remove.textContent = '✕';
  remove.setAttribute('aria-label', `Remove ${source.label}`);
  remove.addEventListener('click', (event) => {
    event.stopPropagation();
    removeSource(source.id);
  });

  source.item.append(source.thumb, name, remove);
  source.item.addEventListener('click', () => select(source.id));
  sourceList.append(source.item);
}

function removeSource(id) {
  const index = sources.findIndex((source) => source.id === id);
  if (index === -1) return;

  const [source] = sources.splice(index, 1);
  source.stream.getTracks().forEach((track) => track.stop());
  source.video.srcObject = null;
  source.item.remove();

  if (activeId === id) activeId = sources[0]?.id ?? null;
  renderState();
}

function select(id) {
  if (!sources.some((source) => source.id === id)) return;
  activeId = id;
  renderState();
}

function renderState() {
  emptyHint.hidden = sources.length > 0;
  sources.forEach((source) => {
    source.item.setAttribute('aria-current', String(source.id === activeId));
  });
  if (sources.length === 0) outputContext.clearRect(0, 0, output.width, output.height);
}

function drawFrame() {
  const active = activeSource();
  drawOutput(active);
  sources.forEach(drawThumbnail);
}

function drawOutput(source) {
  if (!source || !source.video.videoWidth) return;
  const { videoWidth, videoHeight } = source.video;
  if (output.width !== videoWidth || output.height !== videoHeight) {
    output.width = videoWidth;
    output.height = videoHeight;
  }
  outputContext.drawImage(source.video, 0, 0, videoWidth, videoHeight);
}

function drawThumbnail(source) {
  if (!source.video.videoWidth) return;
  source.thumb.getContext('2d').drawImage(source.video, 0, 0, THUMBNAIL_WIDTH, THUMBNAIL_HEIGHT);
}

// Timer runs in a worker so a covered/background tab is not throttled to 1 fps.
function startFrameLoop() {
  const code = `setInterval(() => postMessage(0), ${FRAME_INTERVAL_MS});`;
  const worker = new Worker(URL.createObjectURL(new Blob([code], { type: 'text/javascript' })));
  worker.onmessage = drawFrame;
}

function handleKey(event) {
  if (event.altKey || event.ctrlKey || event.metaKey) return;
  const index = Number(event.key) - 1;
  if (Number.isInteger(index) && sources[index]) select(sources[index].id);
}

// Floating, always-on-top mini panel (Document Picture-in-Picture, Chrome/Edge 116+).
async function openFloatingControls() {
  if (floatingWindow && !floatingWindow.closed) return;
  floatingWindow = await window.documentPictureInPicture.requestWindow({ width: 280, height: 220 });
  floatingWindow.document.head.append(
    ...[...document.querySelectorAll('link[rel="stylesheet"]')].map((link) => link.cloneNode()),
  );
  floatingWindow.document.body.append(document.querySelector('aside'));
  floatingWindow.addEventListener('pagehide', () => {
    document.querySelector('main').append(floatingWindow.document.querySelector('aside'));
  });
  floatingWindow.addEventListener('keydown', handleKey);
}

// Optional bridge: a local helper (e.g. the desktop app) sends {"focusedTitle": "..."}
// and the matching source becomes active. Protocol is documented in .claude/docs/web-prototype.md.
function toggleBridge() {
  if (bridgeSocket) {
    bridgeSocket.close();
    return;
  }
  const url = prompt('Focus bridge WebSocket URL', 'ws://127.0.0.1:47800');
  if (!url) return;
  bridgeSocket = new WebSocket(url);
  bridgeSocket.addEventListener('open', () => {
    bridgeButton.setAttribute('aria-pressed', 'true');
    setStatus('Focus bridge connected.');
  });
  bridgeSocket.addEventListener('message', (event) => followFocus(event.data));
  bridgeSocket.addEventListener('close', () => {
    bridgeSocket = null;
    bridgeButton.setAttribute('aria-pressed', 'false');
    setStatus('Focus bridge disconnected.');
  });
  bridgeSocket.addEventListener('error', () => setStatus('Focus bridge error.'));
}

function followFocus(rawMessage) {
  let focusedTitle;
  try {
    ({ focusedTitle } = JSON.parse(rawMessage));
  } catch {
    return;
  }
  if (typeof focusedTitle !== 'string') return;
  const match = sources.find((source) => focusedTitle.includes(source.label) || source.label.includes(focusedTitle));
  if (match) select(match.id);
}

addButton.addEventListener('click', addWindow);
bridgeButton.addEventListener('click', toggleBridge);
floatButton.addEventListener('click', openFloatingControls);
document.addEventListener('keydown', handleKey);
floatButton.hidden = !('documentPictureInPicture' in window);
startFrameLoop();
renderState();
