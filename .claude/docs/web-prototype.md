# Web prototype (`web/`)

Static page, no build. Run: `cd web && python -m http.server 8765`, open `http://localhost:8765` in Chrome/Edge (getDisplayMedia needs a secure context; localhost counts).

## What it does
- "+ Add window": `getDisplayMedia` (one picker per window, `selfBrowserSurface: exclude`). Each stream -> hidden `<video>`.
- Output `<canvas>` draws the active source; the user shares *this tab/window* in Teams/Meet/OBS (same idea as the desktop app's own window).
- Switch source: click thumbnail, keys 1-9, optional floating always-on-top panel (Document Picture-in-Picture, Chrome/Edge 116+), or the focus bridge.
- Frame timer runs in a Web Worker so a covered/background tab is not throttled (rAF would be).

## Limits
- Browser cannot know which OS window has focus -> no automatic follow without the bridge.
- Chrome/Edge desktop only for practical use; user must approve each window; closing a source window ends its track (auto removed).
- Prototype is untested end to end: picker/stream flow needs a human; only page load + feature detection were checked.

## Focus bridge (implemented in the desktop app)
`Domain/Services/FocusBridgeService` (`IFocusBridge`, singleton) runs an `HttpListener` WebSocket server on `ws://127.0.0.1:47800/` (port in `FocusBridgeOptions`). `CapturedWindows` starts it and calls `Publish(window)` from the `MonitoredWindowFocused` handler; it dedupes by HWND, so a message is sent only when the focused picked window changes. New clients immediately get the last message.

Message (text frame):

```json
{ "focusedTitle": "Document1 - Word", "processName": "WINWORD" }
```

Page picks the source whose `track.label` contains (or is contained in) `focusedTitle`. Label format of window tracks is browser-defined, so matching is heuristic.

Security: loopback only; only **picked** windows are ever published; `Origin` must be loopback (`localhost`/`127.0.0.1`) or listed in env var `CAPTURE_MANAGER_ALLOWED_ORIGINS` (comma separated, e.g. a hosted site); non-WebSocket requests get 426, bad origin 403; requests without Origin (non-browser) are accepted since local processes can read titles anyway. If the port is taken or listening fails, the bridge logs and stays off; the app keeps working.

Verified: server starts without admin, loopback origin and no-origin connect, foreign origin rejected. Not verified: actual focus messages and page auto-switch end to end (needs a picked window and a human).

Still polling: focus detection is the existing 10 ms poll; `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` (known-issues #11) remains a future improvement.
