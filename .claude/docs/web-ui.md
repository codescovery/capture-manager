# Web UI (`src/Web/CodescoveryCaptureManager.Web`)

React 18 + TypeScript + Vite app, part of the solution tree. The desktop app builds it and **serves it itself** at `http://127.0.0.1:47800/` (same server as the focus bridge). Open that URL in Chrome/Edge (use `127.0.0.1`, not `localhost`: `HttpListener` matches the Host header to its prefix; `127.0.0.1` is a secure context so `getDisplayMedia` works).

Replaces the old plain HTML/JS prototype in `web/` (removed).

## What it does
- "+ Add window": `getDisplayMedia` (one browser picker per window, `selfBrowserSurface: exclude`). Each stream feeds a hidden `<video>`.
- `Stage` canvas draws the shown source; the user shares **this tab** in Teams/Meet/OBS. A Web Worker timer (`lib/frameClock.ts`) drives drawing so a covered tab is not throttled.
- **Follows the desktop focus** through the bridge (auto-connect + reconnect every 2 s). Options: follow on/off, "when no selected window is focused": show a message (default) / keep the last window (saved in `localStorage`). Picking a source by click or key 1-9 switches to manual mode.
- Same semantics as the desktop: blur (a foreign unpicked window is focused) -> blank + "No selected window is focused"; a focused picked window that has no source in the browser -> blank + "not added here" (never keeps the last one: could be another window).
- **Output only** (button, Esc leaves): hides panel and thumbnails so thumbnails of other windows never go on air. **Float controls**: Document Picture-in-Picture (Chrome/Edge 116+) hosts the panel through a React portal (`hooks/useFloatingPanel.tsx`), so the shared tab can stay in output-only mode.
- Source names are editable (double-click) and used as a title fallback when matching.

## Matching browser sources to the focused window
Bridge sends `handle` (HWND) and `focusedTitle`. Chrome window tracks are labelled `window:<native id>:<n>`; on Windows the id is believed to be the HWND, so `lib/focus.ts` matches `handle` first, then title/name overlap (min 3 chars, case-insensitive). **Not verified with a real window capture** (needs a human at the picker): if the id is not the HWND, matching falls back to titles and the user-given name.

## Focus bridge protocol (desktop `FocusBridgeService`)
`ws://127.0.0.1:47800/` (text frames). New clients immediately receive the last message.

```json
{ "focused": true, "focusedTitle": "Document1 - Word", "processName": "WINWORD", "handle": 1180 }
{ "focused": false }
```

- Focus message published from `MonitoredWindowFocused` (deduped by HWND); `{"focused":false}` from `MonitoredWindowsBlurred` (`IFocusBridge.PublishBlurred`, deduped). Initial state is blurred.
- Old clients that only read `focusedTitle` keep working (legacy message had no `focused`/`handle`).

Security: loopback only; only **picked** windows are published; WebSocket `Origin` must be loopback or in env var `CAPTURE_MANAGER_ALLOWED_ORIGINS`; non-browser clients (no Origin) allowed. Static site: GET/HEAD only, files only under `<app dir>/web`, path traversal blocked, `nosniff`, `no-cache`; if `web/index.html` is missing, plain requests get 426 as before. If the port is taken the bridge logs and stays off; the app keeps working.

## Serving (`Domain/Services/StaticSiteResponder`)
`FocusBridgeOptions.WebDirectoryName` (`web`) next to the exe. SPA fallback: unknown path without extension returns `index.html`.

## Build integration
`CodescoveryCaptureManager.App.csproj` target `BuildWebUi` (before `AssignTargetPaths`): `npm ci` when `node_modules` is missing, `npm run build` (`tsc --noEmit && vite build`), then copies `dist/**` to `web/` in the output and publish folders. Needs Node 22. `-p:SkipWebBuild=true` builds the desktop app without Node (a warning says the page will not be served). CI and release workflows run `actions/setup-node@v4` (22).

## Dev loop
```bash
cd src/Web/CodescoveryCaptureManager.Web
npm install
npm run dev     # Vite on :5173, connects to the running desktop app at ws://127.0.0.1:47800
npm test        # vitest: lib/focus.ts (parsing + matching)
```
Allow the dev origin if needed: Vite on `127.0.0.1` is loopback, so it is accepted by default.

## Limits
- Browser cannot know the OS focus itself; without the desktop app the page is manual (badge says so).
- Chrome/Edge desktop only; the user must approve each window; closing a source window ends its track (removed automatically).
- Profiles are desktop-only (rules need process/title data the browser does not have).
- Still polling: focus detection is the desktop 10 ms poll (`SetWinEventHook` = known-issues #11).
- End to end capture + auto-switch with real windows not verified; verified: build, 10 unit tests, page served by the running app, WebSocket initial `{"focused":false}`, traversal blocked, UI renders and connects.
