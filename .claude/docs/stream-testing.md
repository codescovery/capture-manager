# Stream testing (latency, frame rate, viewers, encoders)

How the web stream is measured, and what was measured on 2026-11-18.

## Rig (`.claude/scripts/streamrig`)
- `ccm-streamrig` (WinForms, `Program.cs`): window titled `ccm-stream-rig`. The client area flips black/white every 233 ms and a bar moves once per compositor frame (`DwmFlush`), so the window changes at the display rate. Each flip is time-stamped when painted. Serves on `http://127.0.0.1:47811/`: `/time` (its clock), `/toggles`, `/status` (foreground, paints per second, largest gap between paints since the last call), `/focus`, `/quit`, and the page `rig.html`.
- `rig.html`: opens N WebSocket viewers to the app (`ws://127.0.0.1:47800`), decodes with WebCodecs using the same decoder settings as the real page, reads the luma of one pixel of every decoded frame (`VideoFrame.copyTo` with a 2x2 rect), and matches each black/white change to the rig's flip. Clocks are aligned with the best of 150 `/time` round trips (error is half the round trip, 0.5 to 1.2 ms in these runs). Reports per viewer: frames, fps, Mbps, key frames, frame gap percentiles, and latency split in paint -> received over the socket -> decoded.
- `run.sh [width] [height] [ENV=value ...]`: builds app and rig, starts the app hidden in the tray with an isolated data folder (profile bound to the rig, fallback `KeepLast` so the stream survives the rig losing focus), starts the rig, prints the app log. Then open `http://127.0.0.1:47811/?viewers=1&seconds=15` (or `?autostart=0` and call `runRig(viewers, seconds)` from the console).

What "latency" means here: from the rig finishing its paint to the browser's decoder handing out the frame. Not included: the browser drawing the canvas and its compositor (up to one more refresh), and the decode happens in an embedded Chromium, not a stand-alone Chrome/Edge.

Limits of the rig: content is trivial (flat colour plus a small bar, 0.3 to 0.6 Mbps), so encoder load and bitrate under real content are not exercised. A window larger than the screen is captured at full size, but GDI only repaints the on-screen part (the page samples a point at 1/4, 1/4 of the frame for that reason).

## Results (RTX 5060 Ti, 1920x1080 @ 164 Hz display, Windows 11 26200)

| Case | Stream | fps | Latency p50 / p90 / max (ms) | Notes |
|------|--------|-----|------------------------------|-------|
| 720p, hardware, before the fixes | 1282x752 | 56 | 19.0 / 20.7 / 21.9 | capture delivered 55 fps = every third refresh |
| 720p, hardware | 1282x752 | 84 | 11.7 / 14.9 / 115 | max is one sample at stream start |
| 1080p, hardware | 1922x1112 | 79 to 83 | 13.5 to 15.8 / 18.3 to 20.5 / 22.6 to 140.7 | four 15 s runs on the final build, 63 samples each |
| 4K window, hardware | 3842x2172 | 82 | 18.8 / 22.9 / 49 | level 5.2 (`avc1.4d4034`), no errors |
| 720p, software, before the fix | 1282x752 | 83 | 229.7 / 235.3 / 245 | encoder held about 18 frames |
| 720p, software | 1282x752 | 79 | 18.4 / 26.2 / 42 | after setting codec options before the media types |
| 3 viewers (one page), 720p | 1282x752 | 76 each | 12.3 to 16.8 / 19.7 to 25.4 | 902 to 905 frames each, 0 decode errors |
| 3 viewers (2 rig + real page), 1080p | 1922x1112 | 83 each | 14.2 / 18.6 / 24 | real page showed `1922x1112 · 82 fps · 0.3 Mbps` |

Decode (received -> decoded) was 0.6 to 0.9 ms p50 in every hardware case with one viewer per page. With several decoders in one page the extra milliseconds are the page's main thread, not the app.

Also seen working: the app captured and streamed while hidden in the tray (all runs), and the stream kept going in `KeepLast` mode with the rig window not focused.

## What the measurements changed
1. **Capture was paced at a third of the display rate.** Windows Graphics Capture delivered 55 frames/s for a window updating 164 times/s. `GraphicsCaptureSession.MinUpdateInterval` (build 26100+) removes that: capture now arrives at 160 to 165 frames/s (`Helpers/CaptureSessionPacing`, set through the ABI because the project targets the 19041 projection). On older Windows the call is a no-op and the default pacing stays. Latency fell from 19 to about 12 ms at 720p because a frame waits less for the next capture.
2. **Frame pool is free threaded** (`Direct3D11CaptureFramePool.CreateFreeThreaded`): frames no longer go through the UI thread. Evidence is weak: one 652 ms capture gap in 32 s with the UI-thread pool, none above 32 ms in 48 s with the free-threaded pool, single runs on a machine in use.
3. **Software encoder latency**: codec options (low latency, rate control, bitrate) are now set before the media types. Set afterwards, the Microsoft software encoder accepted low latency without effect.
4. **Hardware failure falls back to software** (`H264Encoder.Create`): a hardware encoder that is listed but fails to start no longer leaves the stream dead. Not seen happening for real; only the forced path (`CAPTURE_MANAGER_SOFTWARE_ENCODER=1`) was run.
5. `Encoder event failed` was logged as an error on every normal stop; now silent when stopping.

## Open
- **82 fps on a 164 Hz display**: the 120 fps cap (`VideoStreamService.MaxFramesPerSecond`) drops every second frame at 164 Hz (and gives 72 at 144 Hz, 120 at 240 Hz). Even cadence, but below the cap. Left as is.
- The encoder is told 60 fps (`FramesPerSecond`) while it receives up to 120, so the rate control spends more bits than the target on complex content. Not measured (rig content is trivial).
- Occasional gaps of 40 to 650 ms in the stream, on a machine in use. In four runs where the rig recorded its own paint gaps: two had no gap above 21 ms, one matched the rig (stream 40.7 ms, rig 42.3 ms: the source window stalled), one did not (stream 136 ms, rig 74 ms). So part of it is the source, part is not attributed (app, GPU, or the browser).
- Not tested: real content (video, scrolling text) at 1080p/4K, sources above 4096 px (downscale path), a machine without a hardware encoder, a second GPU vendor, viewers in separate browser windows or on a slow machine, latency in stand-alone Chrome/Edge, long runs (memory, drift), Windows before 24H2.
