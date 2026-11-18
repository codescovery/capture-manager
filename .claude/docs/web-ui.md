# Web UI (`src/Web/CodescoveryCaptureManager.Web`)

React 18 + TypeScript + Vite app, part of the solution tree. **It is a viewer of the desktop app**: Capture Manager captures, hardware-encodes to H.264 and streams the window it is showing; the page decodes with WebCodecs and paints it. The desktop app builds the page, embeds it in the Domain assembly and serves it at `http://127.0.0.1:47800/` (same server as the focus bridge). Open that URL in Chrome/Edge (use `127.0.0.1`, not `localhost`: `HttpListener` matches the Host header to its prefix).

Replaces the earlier plain HTML/JS prototype (`getDisplayMedia` in the browser, removed): the browser no longer captures anything and picks no windows.

## What the page does
- Connects to `ws://<same host>` (reconnects every 2 s). Text frames = focus state, binary frames = video.
- `lib/videoPlayer.ts`: on each key frame reads the SPS from the Annex-B access unit (`lib/annexb.ts`) -> codec string `avc1.PPCCLL` -> `VideoDecoder.configure({optimizeForLatency, prefer-hardware, avc:{format:'annexb'}})`. Every decoded `VideoFrame` is drawn to the canvas immediately (no rAF, no buffer). If the decode queue grows (>4) it drops until the next key frame and sends the text message `key` to ask for one.
- Same semantics as the desktop: `{focused:false}` -> blank + "No selected window is focused" (or keep the last picture, setting saved in `localStorage`); frames received while blurred are dropped. Offline desktop app -> "not running" message.
- **Output only** (Esc leaves) hides panel/header so only the video is in the shared tab. **Float controls**: Document Picture-in-Picture (Chrome/Edge 116+) hosts the panel through a React portal (`hooks/useFloatingPanel.tsx`); the in-page panel is hidden while floating.
- Panel shows connection state, focused window title and live `WxH · fps · Mbps`.
- Which windows are shared is chosen only in the desktop app (picker / profiles). The web has no source list.

## Stream (desktop side, `Domain/Streaming`)
```
CaptureService.ProcessFrame ──IFrameTap.OnFrame(texture, w, h)──▶ VideoStreamService
   Nv12Converter: D3D11 video processor BGRA -> NV12 (BT.709), scales >4096 px, ring of 8 NV12 textures (GPU only)
   H264Encoder: Media Foundation H.264 encoder MFT
        hardware (async MFT, D3D11 device manager, NeedInput/HaveOutput events) else Microsoft software encoder (sync, NV12 read back to CPU)
        low latency, no B-frames, bitrate = w*h*60*0.12 (4-40 Mbps), key frame on request (ICodecAPI ForceKeyFrame)
   ──EncodedFrame(Annex-B, isKey)──▶ FocusBridgeService ──binary WebSocket──▶ browser
```
- **Zero work without viewers**: `VideoStreamService` does nothing until `FocusBridgeService` reports >= 1 client (`SetViewerCount`); pipeline is created lazily on the first frame, rebuilt on content size change, torn down when capture stops (`CaptureService.Dispose -> OnCaptureStopped`) or the last viewer leaves.
- Capture rate = display rate on Windows 11 24H2+ (`CaptureSessionPacing` sets `MinUpdateInterval`; older Windows: the default pacing, measured at a third of a 164 Hz display). Frames are then rate-limited to 120 fps by dropping the ones that come too soon, which on a 164 Hz display means every second frame (82 fps). `Submit` keeps only the latest frame (latest wins) so the encoder never lags behind.
- Encoder choice (`H264Encoder.Create`): hardware first; if it is missing or fails to start, the Microsoft software encoder. Env var `CAPTURE_MANAGER_SOFTWARE_ENCODER=1` forces software. Codec options are set before the media types (the software encoder ignores low latency otherwise).
- Per-client backpressure (`ClientConnection.TrySendVideo`): if a client's previous send is still pending the frame is dropped for that client, delta frames are skipped until the next key frame and a key frame is requested. Text messages are queued in order on the same per-client lock.
- Binary frame layout: `[0]=1 (video)`, `[1]` bit0 = key frame, `[2..]` H.264 Annex-B access unit (SPS/PPS + IDR in key frames, AUD present).
- Raw-vtable calls (`MediaFoundationNative`): `IMFTransform::ProcessOutput` (SharpDX does not copy the MFT-provided sample back) and `ICodecAPI::SetValue` (RCW marshaling fails from the encoder thread: E_NOINTERFACE). Vtable indices: ProcessOutput 25, ICodecAPI.SetValue 9. `MFShutdownObject` (`mf.dll`) unblocks the event loop on dispose.
- `Direct3D11Helper.CreateDevice`: `BgraSupport | VideoSupport` and multithread protection (capture, converter and encoder threads share the device).

## Bridge protocol (`FocusBridgeService`)
`ws://127.0.0.1:47800/`. New clients immediately get the last text message, then a key frame is requested.

```json
{ "focused": true, "focusedTitle": "Document1 - Word", "processName": "WINWORD", "handle": 1180 }
{ "focused": false }
```
Client -> server: the text `key` = request a key frame (only message accepted).

- Focus text from `MonitoredWindowFocused` (deduped by HWND); blur from `MonitoredWindowsBlurred` (`IFocusBridge.PublishBlurred`). Initial state is blurred. Publishing a new focus also requests a key frame.
- Security: loopback only; only the picked window being shown is ever streamed (nothing else leaves the process); WebSocket `Origin` must be loopback or in env var `CAPTURE_MANAGER_ALLOWED_ORIGINS`. Static site: GET/HEAD, embedded resources only, `nosniff`, `no-cache`; missing `web/index.html` resource -> plain requests get 426. Port taken -> bridge logs and stays off.

## Serving and build
- `StaticSiteResponder` reads resources `web/<path>` from the Domain assembly; unknown path without extension -> `index.html` (SPA).
- `CodescoveryCaptureManager.Domain.csproj` target `BuildWebUi`: `npm ci` if needed, `npm run build` (`tsc --noEmit && vite build`), `dist/**` -> `EmbeddedResource` `LogicalName="web/<path>"` (backslashes normalized). Needs Node 22; `-p:SkipWebBuild=true` skips it (page then not served). CI/release run `actions/setup-node@v4`.

## Dev loop
```bash
cd src/Web/CodescoveryCaptureManager.Web
npm install
npm run dev     # Vite :5173, connects to the running desktop app at ws://127.0.0.1:47800
npm test        # vitest: lib/focus.ts, lib/annexb.ts
```

## Verified / not verified
Verified on this machine (hardware H.264 encoder found): app serves the page; stream reaches a Node client and a browser tab; browser decodes and draws (`1266x712 · 55 fps · 1.6 Mbps`, animation visible); blur -> page blanks; key frame roughly every second from the hardware encoder; 25 C# + 8 vitest tests. Frame cadence equals the source's (animated WinForms window, ~55 fps).
Measured 2026-11-18 with the stream rig (details and limits in stream-testing.md): paint -> decoded latency p50 12 ms at 720p, 14 to 16 ms at 1080p, 19 ms for a 4K window (hardware), 18 ms with the software encoder at 720p; 82 to 84 fps on a 164 Hz display; three simultaneous viewers each got every frame with no decode error; streaming works with the app hidden in the tray.
Not verified: real content (the rig's picture is trivial), sources above 4096 px, a machine without a hardware encoder, Windows before 24H2, stand-alone Chrome/Edge latency. `B-frame count` is rejected by the hardware encoder and the low-delay rate control mode by the software one (0x80070057, harmless, logged as warnings).

## Limits
- Chrome/Edge desktop (WebCodecs); H.264 only.
- Profiles are desktop-only.
