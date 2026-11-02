# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project purpose

CodescoveryCaptureManager: Windows WPF app for streamers/presenters. User picks which top-level windows to expose; the app captures them (Windows Graphics Capture) and renders only the **currently focused picked window** inside its own single window. In Teams/OBS/etc. you share *this one app window* instead of the whole screen, so unpicked windows never leak. Goal now: make it more resilient and functional.

## Docs are source of truth (mandatory workflow)

- All Claude-related material lives under `.claude/` (docs, skills, plans, instructions, agents). Nothing Claude-specific at repo root or in `src/`.
- Start every investigation in [.claude/docs/README.md](docs/README.md) -> [architecture-map.md](docs/architecture-map.md) before grepping code. Docs answer first; code confirms.
- **Every feature/change/bugfix: before finishing, check whether a doc needs adding or editing** (architecture map, known-issues, decisions) and update it in the same change. If code and docs disagree, fix the doc (or the code) — never leave drift.
- Plans go in `.claude/plans/`, reusable skills in `.claude/skills/`.

## Commands

Run from `src/`. Solution: `Codescovery.CaptureManager.sln`. Target `net10.0-windows10.0.19041.0`, Windows only (WPF, CsWinRT-projected WinRT, user32); needs .NET 10 SDK and Node 22 (the Domain build runs `npm` for `src/Web`, the React UI the app serves at `http://127.0.0.1:47800/`; skip with `-p:SkipWebBuild=true`, see docs/web-ui.md). xUnit tests in `src/Tests` (profile logic), no linter; CI = `.github/workflows/ci.yml` (build + test). A running app instance locks `bin\Debug` DLLs: close it before building. UI behaviour is checked with the UI Automation scripts in `.claude/scripts` (see docs/e2e-testing.md).

```bash
dotnet restore src/Codescovery.CaptureManager.sln
dotnet build   src/Codescovery.CaptureManager.sln
dotnet test    src/Codescovery.CaptureManager.sln
dotnet test    src/Tests/CodescoveryCaptureManager.Domain.Tests --filter "FullyQualifiedName~WindowRuleMatcherTests"   # single test class
dotnet run --project src/Presentation/CodescoveryCaptureManager.App
```

Debug start project: `Presentation/CodescoveryCaptureManager.App`.

## Architecture (short; full map in docs)

Two projects: `Presentation/…App` (WPF, MaterialDesign UI, DI composition root `Startup.cs`) and `Domain/…Domain` (window enumeration, focus monitoring, WinRT capture + SharpDX/Composition rendering, P/Invoke). Despite the name, Domain holds infrastructure/interop too and references WPF.

Core flow: `SelectCapturableWindows` picker -> `ICapturableWindowsMonitoringService` (HWND dictionary + 10 ms foreground poll) -> `MonitoredWindowFocused` event -> `CapturedWindows` creates `GraphicsCaptureItem` -> `WindowCaptureService`/`CaptureService` copy frames to a swap chain shown through a Windows.UI.Composition visual bound to the WPF window HWND.

Non-obvious things:
- `App` creates a `DispatcherQueueController` (CoreMessaging.dll) on the UI thread; Composition needs it. Don't remove.
- `Startup.ServiceProvider` is cached (one provider); Scoped services are effectively singletons from the root. `IProfileService`, `IFocusBridge` are singletons.
- `CapturableWindowsMonitoringService` and `WindowsProcessServices` are `internal`; only reachable via DI interfaces. `WindowCaptureService` is public and `new`ed by the UI, not injected.
- The web page is a viewer: `Domain/Streaming` hardware-encodes the shown window (H.264) and `FocusBridgeService` streams it (docs/web-ui.md). Media Foundation calls that SharpDX cannot do go through raw vtables in `MediaFoundationNative`; keep them off RCW marshaling (apartment problems).
- Windows identified purely by HWND; no handling of closed/recreated windows yet (see known-issues).

## Doc index

See [docs/README.md](docs/README.md).
