# Architecture map

Snapshot of code under `src/` (branch `feature/previewSelectWindow`). Paths relative to `src/`.

## 1. Purpose
Share one app window (this app) instead of the screen. User selects N source windows; app shows only the focused one. Share/OBS captures the app window ("Windows Graphics Capture" method in OBS).

## 2. Projects

| Project | Role |
|---------|------|
| `Presentation/CodescoveryCaptureManager.App` (WinExe, net10.0-windows10.0.19041.0, WPF) | UI, DI composition root, window lifecycle. Deps: MaterialDesignThemes 5.2.1, System.Drawing.Common |
| `Domain/CodescoveryCaptureManager.Domain` (lib, net10.0-windows10.0.19041.0, WPF) | Everything else: services, WinRT capture, SharpDX, P/Invoke, VirtualDesktop. Deps: M.E.DependencyInjection, SharpDX(+D3D11,D3DCompiler), VirtualDesktop(+.WPF) |

App -> Domain only. Domain is not pure domain: it holds infrastructure and references WPF types (`Window`).

## 3. Component map

```
App.xaml.cs ── new Startup(this) ── DI: AddCapturableWindowsMonitoringService, AddWindowsProcessServices, CapturedWindows(transient)
   │  CoreMessagingHelper.CreateDispatcherQueueControllerForCurrentThread()
   ▼
CapturedWindows (main window, Windows/CapturedWindows.xaml[.cs])
   ├─ owns Compositor + ContainerVisual + WindowCaptureService (new, not DI)
   ├─ OnLoaded: Compositor.CreateDesktopWindowTarget(own HWND) ← root visual ← WindowCaptureService.GetVisual()
   ├─ ctor → StartCapturing() → ICapturableWindowsMonitoringService.StartMonitoring() (fire-and-forget async loop)
   ├─ subscribes MonitoredWindowFocused → CreateItemForWindow(hwnd) → WindowCaptureService.StartCapture
   ├─ chrome: native frame (`WindowChrome` CaptionHeight 40, Win11 rounded corners/shadow/snap) with our own title bar: app name, status pill (Not set up / Waiting / Standby = picked but none focused / Live / Paused = user's pause), `Windows` (picker), `Profiles` menu, pause button (`SetPaused`: stops capture, ignores focus, overrides fallbacks, tells viewers; also tray item and global hotkey via `Helpers/GlobalHotkey` + `Domain/Hotkeys/Hotkey`), clean view (Ctrl+H: hides title bar + status bar, leaves a slim `Show controls` strip), settings, caption buttons. Maximized: `RootBorder` padding 7 (frame overflow)
   ├─ status bar: what is shown (`Showing <window> · WxH`), viewers, `Streaming WxH · fps` (1 s `DispatcherTimer` over `IFocusBridge.ViewerCount` + `IVideoStream.Stats`), web viewer address (open / copy). `RefreshView()` = single place for overlays + pill + status text
   ├─ capture area: the Composition container visual is sized/offset to `ScreenCaptureGrid` in pixels (`UpdateCaptureBounds`, on SizeChanged/StateChanged) because the visual is drawn over WPF content; drag + double-click-maximize on it
   ├─ `SettingsWindow` (modal, sections General / When nothing is focused / Web viewer / About; applies immediately, no Save): share title (persisted `AppSettings.WindowTitle`), tray, start with Windows, virtual desktop, fallback mode *Show a message* / *Keep the last window* / *Always show one window* (fixed window = live handle, not persisted; closed fixed window falls back to message), web viewer address/state, version + GitHub/issue/releases links. Talks to `CapturedWindows` through its `internal` members (`SetFallback`, `ShareTitle`, `PickedWindows`, ...)
   ├─ `Themes/Theme.xaml` (merged in App.xaml): palette brushes, text styles, `PrimaryButton`/`SecondaryButton`/`GhostButton`/`IconButton`/`CaptionButton`, `Card`. Every window uses these; MaterialDesign stays for text boxes, switches, radios, menus
   ├─ Tray (`App/Tray`): `TrayIconController` (H.NotifyIcon.Wpf; open window / web viewer / profiles / about / exit), `SingleInstance` (named mutex + show event; 2nd launch shows the 1st), `StartupRegistration` (HKCU Run, `--tray` starts hidden). `OnClosing` hides the window unless `App.ExitRequested` (tray Exit, session end) or `AppSettings.ExitOnClose`; capture, bridge and stream keep running hidden
   ├─ EmptyState overlay (three-step guide + `Choose windows` / `Open web viewer`) shown while `HasMonitoredWindows()` is false; NoFocusState overlay ("No selected window is focused") shown when windows are picked but nothing is being captured (blurred/closed). Both refreshed via `RefreshView()` on load, picker close, window closed, blurred, capture start
   └─ OpenCapturePicker → new SelectCapturableWindows(...).Pick(this)

SelectCapturableWindows (picker, Windows/SelectCapturableWindows.xaml[.cs])
   ├─ resizable, owner = main window; header + search box, WrapPanel of cards (ItemsControl + DataTemplate), footer (Select all / Clear / count / Cancel / Apply)
   ├─ IWindowsProcessServices.GetOpenedWindows(ignore self + main) → `WindowPickerItem` (INotifyPropertyChanged: title, process, icon, thumbnail, IsSelected); monitored first, then by title
   ├─ `Helpers/WindowThumbnailProvider`: per-window thumbnail via `PrintWindow(PW_RENDERFULLCONTENT)` + app icon, on thread pool (max 4 parallel), frozen BitmapSource, cancelled on close. If PrintWindow fails (Windows Terminal) falls back to one Graphics Capture frame (`Domain/Helpers/WindowSnapshotHelper`); minimized windows (no frame possible) show a "Minimized" placeholder. Brand green `#8DC63F` (from `Assets/icon.svg`) overrides the theme primary in `App.xaml` (`BrandBrush`, `PrimaryHueMidBrush`)
   ├─ selection starts = currently monitored set; search filters via ICollectionView; Select all/Clear act on visible cards
   └─ Apply (enabled only if something changed, Ctrl+Enter): add newly selected, remove deselected, one StopCapture + SyncWithForegroundWindow (shows a window only if it is really the foreground one). Ctrl+F focuses search

Domain/Services
   ├─ FocusBridgeService (internal, IFocusBridge): loopback WebSocket server serves the React web UI (embedded `web/*` resources), publishes the focused picked window / blur (text) and the H.264 video of the shown window (binary) to browser pages (see web-ui.md)
   ├─ VideoStreamService (internal, IVideoStream/IFrameTap, Domain/Streaming): receives each captured frame from `CaptureService`, converts on the GPU (Nv12Converter), hardware-encodes H.264 (H264Encoder, Media Foundation) and raises `FrameEncoded` for the bridge; idle without viewers (see web-ui.md)
   ├─ WindowsProcessServices (internal, IWindowsProcessServices): EnumWindows → visible + titled + not ignored process
   ├─ CapturableWindowsMonitoringService (internal, ICapturableWindowsMonitoringService): Dictionary<HWND,CapturableWindow>, foreground WinEvent hook (`ForegroundWindowWatcher`) + 1 s safety check; raises `ForegroundWindowChanging` so `ProfileService` can rebind (see decisions 2026-11-18)
   ├─ WindowCaptureService (public): Composition visual tree (root → sprite w/ surface brush + drop shadow), owns D3D device, current CaptureService
   ├─ CaptureService (internal): GraphicsCaptureItem + free-threaded Direct3D11CaptureFramePool (frames handled on a capture thread under `_sync`, not the UI thread) + `CaptureSessionPacing` (display-rate capture on 24H2+) + SharpDX SwapChain1; copies frame → back buffer → Present; resizes on content size change
   └─ ProfileService (internal, IProfileService, singleton) + JsonProfileStore (IProfileStore): saved window sets as rules; see profiles.md

Domain/Profiles: WindowRule, CaptureProfile, AppSettings, WindowRuleMatcher (pure), WindowRuleFactory, WindowTitles
Domain/Diagnostics: AppLog (static, no-op until Start), RollingFileLog (size rotation, repeat collapse). App/Helpers/CrashReporter.
Tests: Tests/CodescoveryCaptureManager.Domain.Tests (xUnit; matcher, factory, store, service, log). CI runs them.
```

UI for profiles: toolbar **Profiles** button (ContextMenu rebuilt on open: profiles, "Save current windows as profile…", "Delete profile ▸"); picker footer "Save as profile…"; `SaveProfileDialog` (+ `RuleRow` edit model). Applying a profile = `ProfileService.Apply` (replace monitored set) then StopCapture + SyncWithForegroundWindow. Picker Apply clears the active profile (manual selection). Active profile + fallback mode (message/keep last) persist in settings.json and the last profile is re-applied on start (`CapturedWindows_OnLoaded`).

## 4. Runtime flows

**Startup**: `App()` → `Startup` (DI) + DispatcherQueueController → `App_OnStartup` resolves `CapturedWindows` → `Show()`. Ctor starts monitoring (nothing monitored yet, loop idles).

**Pick windows**: menu → `OpenCapturePicker` → picker enumerates windows (ignores `applicationframehost, shellexperiencehost, systemsettings, winstore.app, searchui, explorer`, hidden, untitled, self) → click card toggles `IsSelected` → Apply: diff selection vs monitored set (add/remove), then StopCapture + SyncWithForegroundWindow once.

**Focus follow** (`StartMonitoring`): installs `ForegroundWindowWatcher` (`SetWinEventHook` EVENT_SYSTEM_FOREGROUND + MINIMIZEEND, callbacks on the UI thread) and loops a safety check every `CapturableWindowDefaultValues.SafetyCheckInterval` (1 s). Both call `CheckForegroundWindow`: `PruneClosedWindows` (-> `MonitoredWindowClosed`), then `EvaluateForeground(GetForegroundWindow())`: foreign window -> `ForegroundWindowChanging` (profile rebind: attach/release); monitored -> `MonitoredWindowFocused` (raised on every check, subscribers dedupe by HWND); foreign and not monitored -> `MonitoredWindowsBlurred` once; own-app window -> nothing (neutral).

**Capture**: `CaptureHelper.CreateItemForWindow(hwnd)` (IGraphicsCaptureItemInterop) → `CaptureService` creates SharpDX device from WinRT IDirect3DDevice (`Direct3D11Helper`), SwapChain1 (B8G8R8A8, flip-sequential, 2 buffers, premultiplied), frame pool (2 buffers); `FrameArrived` copies frame texture to back buffer and `Present(0)`; on size change resizes buffers and recreates pool. Surface exposed via `Compositor.CreateCompositionSurfaceForSwapChain` → `CompositionSurfaceBrush`.

**Virtual desktop** (optional, `VirtualDesktop.IsSupported`): move app window to a new virtual desktop so the streamer's other desktop stays separate; close removes it.

**Shutdown**: window Closing → StopCapturing + Dispose (disposes monitoring & process services).

## 5. Threading / platform
- Everything effectively on UI thread: WinEvent callbacks and the safety loop (`await Task.Delay`) both run on the WPF UI thread; event handlers touch Composition from UI thread. `FrameArrived` runs on frame-pool thread (default pool created on UI thread w/ dispatcher queue → UI thread).
- Requires Windows 10 1903+ (Graphics Capture), CoreMessaging.dll, Composition desktop interop.
- App manifest: `asInvoker` — cannot capture elevated windows when not elevated.

## 6. DI details
`Domain/Extensions/ServiceConfiguration.cs`: `InjectService<…>` helpers switching on `ServiceLifetime` (default Scoped). Monitoring service gets a singleton `Dictionary<IntPtr,CapturableWindow>`. `Startup.ServiceProvider` is cached (built once). `IFocusBridge` is a singleton.

## 7. Interop surface (`Domain/Interop`, `Interfaces/I*Interop`)
`NativeMethods`: EnumWindows, GetWindowText, IsWindowVisible, GetWindowThreadProcessId, GetWindowRect, GetForegroundWindow, CreateDispatcherQueueController, CreateDirect3D11DeviceFromDXGIDevice, plus unused GDI (BitBlt, PrintWindow …). COM interfaces: `IGraphicsCaptureItemInterop`, `ICompositorDesktopInterop`, `ICompositorInterop`, `IDirect3DDxgiInterfaceAccess`, `IInitializeWithWindow`.

## 8. Dead / unused code
None known. Removed 2026-11-18 after checking for references: `Events/MonitoredWindowsEvents`, `Helpers/OperatingSystemHelper`, `Enums/OperatingSystemType`, `Entities/HexColor`, `Extensions/CommonExtensions`, `Extensions/WindowExtensions`, the commented-out tray registration in `Startup`. `Structs/RECT` stays: `NativeMethods.GetWindowRect` (picker thumbnails) uses it.

## 9. Project-level facts
- Logging: `Domain/Diagnostics` (`AppLog` static facade over `RollingFileLog`), crash handlers in `App/Helpers/CrashReporter`; see logging.md. No `Console.WriteLine` (WinExe).
- User-facing documentation is in two languages with the same structure: `README.md` (English) and `README.pt-BR.md` (Portuguese), linked to each other in their first lines. **Change both in the same commit.** UI labels stay in English in the Portuguese text (the app is not translated). `App/README.md` only points to the root README.
- README animations: `docs/images/<scene>.<en|pt>.gif` (scenes `focus`, `pause`, `web`; captions are baked in, hence one file per language). Regenerate with `bash .claude/scripts/demo-recorder/record.sh [scene ...]` (needs ffmpeg and Edge) after a visible UI change. The recorder (`ccm-demo`, WinForms) opens three demo windows with made-up content, starts the app with a profile bound to two of them, and draws each frame from `PrintWindow` pictures of those windows, the app window and a guest-mode Edge window: the screen is never captured. It takes the foreground while it runs (about 15 s per scene and language).
- One solution: `src/Codescovery.CaptureManager.sln`. `.gitignore` covers `.vs/`, `.idea/`, `*.user`, `bin/`, `obj/`.
