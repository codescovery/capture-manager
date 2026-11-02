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
   ├─ toolbar (Border, not Menu): editable title, picker, settings ContextMenu (virtual desktop), hide-toolbar, min/max/close; drag on toolbar background or capture area
   ├─ settings ContextMenu → "When no selected window is focused": *Show a message* (default) / *Keep the last window* / *Always show: <picked window>* (list rebuilt on open from `GetMonitoredWindows()`). Chosen in `_fallbackMode`; applied on `MonitoredWindowsBlurred` (`ApplyFocusFallback`) and immediately if already blurred (`IsBlurred`). A closed fixed window falls back to message. Not persisted across restarts
   ├─ EmptyState overlay ("Choose windows") shown while `HasMonitoredWindows()` is false; NoFocusState overlay ("No selected window is focused") shown when windows are picked but nothing is being captured (blurred/closed). Both refreshed via `UpdateEmptyState()` on load, picker close, window closed, blurred, capture start
   └─ OpenCapturePicker → new SelectCapturableWindows(...).Pick(this)

SelectCapturableWindows (picker, Windows/SelectCapturableWindows.xaml[.cs])
   ├─ resizable, owner = main window; header + search box, WrapPanel of cards (ItemsControl + DataTemplate), footer (Select all / Clear / count / Cancel / Apply)
   ├─ IWindowsProcessServices.GetOpenedWindows(ignore self + main) → `WindowPickerItem` (INotifyPropertyChanged: title, process, icon, thumbnail, IsSelected); monitored first, then by title
   ├─ `Helpers/WindowThumbnailProvider`: per-window thumbnail via `PrintWindow(PW_RENDERFULLCONTENT)` + app icon, on thread pool (max 4 parallel), frozen BitmapSource, cancelled on close. If PrintWindow fails (Windows Terminal) falls back to one Graphics Capture frame (`Domain/Helpers/WindowSnapshotHelper`); minimized windows (no frame possible) show a "Minimized" placeholder. Brand green `#8DC63F` (from `Assets/icon.svg`) overrides the theme primary in `App.xaml` (`BrandBrush`, `PrimaryHueMidBrush`)
   ├─ selection starts = currently monitored set; search filters via ICollectionView; Select all/Clear act on visible cards
   └─ Apply (enabled only if something changed, Ctrl+Enter): add newly selected, remove deselected, one StopCapture + SyncWithForegroundWindow (shows a window only if it is really the foreground one). Ctrl+F focuses search

Domain/Services
   ├─ FocusBridgeService (internal, IFocusBridge): loopback WebSocket server serves the React web UI (embedded `web/*` resources) and publishes the focused picked window / blur to browser pages (see web-ui.md)
   ├─ WindowsProcessServices (internal, IWindowsProcessServices): EnumWindows → visible + titled + not ignored process
   ├─ CapturableWindowsMonitoringService (internal, ICapturableWindowsMonitoringService): Dictionary<HWND,CapturableWindow>, poll loop
   ├─ WindowCaptureService (public): Composition visual tree (root → sprite w/ surface brush + drop shadow), owns D3D device, current CaptureService
   ├─ CaptureService (internal): GraphicsCaptureItem + Direct3D11CaptureFramePool + SharpDX SwapChain1; copies frame → back buffer → Present; resizes on content size change
   └─ ProfileService (internal, IProfileService, singleton) + JsonProfileStore (IProfileStore): saved window sets as rules; see profiles.md

Domain/Profiles: WindowRule, CaptureProfile, AppSettings, WindowRuleMatcher (pure), WindowRuleFactory, WindowTitles
Tests: Tests/CodescoveryCaptureManager.Domain.Tests (xUnit; matcher, factory, store, service). CI runs them.
```

UI for profiles: toolbar **Profiles** button (ContextMenu rebuilt on open: profiles, "Save current windows as profile…", "Delete profile ▸"); picker footer "Save as profile…"; `SaveProfileDialog` (+ `RuleRow` edit model). Applying a profile = `ProfileService.Apply` (replace monitored set) then StopCapture + SyncWithForegroundWindow. Picker Apply clears the active profile (manual selection). Active profile + fallback mode (message/keep last) persist in settings.json and the last profile is re-applied on start (`CapturedWindows_OnLoaded`).

## 4. Runtime flows

**Startup**: `App()` → `Startup` (DI) + DispatcherQueueController → `App_OnStartup` resolves `CapturedWindows` → `Show()`. Ctor starts monitoring (nothing monitored yet, loop idles).

**Pick windows**: menu → `OpenCapturePicker` → picker enumerates windows (ignores `applicationframehost, shellexperiencehost, systemsettings, winstore.app, searchui, explorer`, hidden, untitled, self) → click card toggles `IsSelected` → Apply: diff selection vs monitored set (add/remove), then StopCapture + SyncWithForegroundWindow once.

**Focus-follow loop** (`StartMonitoring`): every `CapturableWindowDefaultValues.RefreshRate` (10 ms) `Task.Delay` → `NativeMethods.GetActiveWindow()` (wraps `GetForegroundWindow`) → if foreground HWND is monitored → raise `MonitoredWindowFocused`. Each tick first prunes HWNDs failing `IsWindow` (raises `MonitoredWindowClosed`; UI stops capture if it was showing that window) and is wrapped in try/catch. UI handler skips if already capturing that HWND, else starts new capture (stops previous). If foreground is a window of **another process** that is not monitored, the service raises `MonitoredWindowsBlurred` once and the UI calls `StopCapture` → the view goes blank (nothing leaks). Foreground in **this app** (picker, capture window, toolbar) is neutral: last view stays. `SyncWithForegroundWindow` (after Apply) raises focus only if a monitored window is the real foreground window; otherwise nothing is shown (never show an unfocused window).

**Capture**: `CaptureHelper.CreateItemForWindow(hwnd)` (IGraphicsCaptureItemInterop) → `CaptureService` creates SharpDX device from WinRT IDirect3DDevice (`Direct3D11Helper`), SwapChain1 (B8G8R8A8, flip-sequential, 2 buffers, premultiplied), frame pool (2 buffers); `FrameArrived` copies frame texture to back buffer and `Present(0)`; on size change resizes buffers and recreates pool. Surface exposed via `Compositor.CreateCompositionSurfaceForSwapChain` → `CompositionSurfaceBrush`.

**Virtual desktop** (optional, `VirtualDesktop.IsSupported`): move app window to a new virtual desktop so the streamer's other desktop stays separate; close removes it.

**Shutdown**: window Closing → StopCapturing + Dispose (disposes monitoring & process services).

## 5. Threading / platform
- Everything effectively on UI thread: poll loop `await Task.Delay` continues on WPF sync context; event handlers touch Composition from UI thread. `FrameArrived` runs on frame-pool thread (default pool created on UI thread w/ dispatcher queue → UI thread).
- Requires Windows 10 1903+ (Graphics Capture), CoreMessaging.dll, Composition desktop interop.
- App manifest: `asInvoker` — cannot capture elevated windows when not elevated.

## 6. DI details
`Domain/Extensions/ServiceConfiguration.cs`: `InjectService<…>` helpers switching on `ServiceLifetime` (default Scoped). Monitoring service gets a singleton `Dictionary<IntPtr,CapturableWindow>`. `Startup.ServiceProvider` is cached (built once). `IFocusBridge` is a singleton.

## 7. Interop surface (`Domain/Interop`, `Interfaces/I*Interop`)
`NativeMethods`: EnumWindows, GetWindowText, IsWindowVisible, GetWindowThreadProcessId, GetWindowRect, GetForegroundWindow, CreateDispatcherQueueController, CreateDirect3D11DeviceFromDXGIDevice, plus unused GDI (BitBlt, PrintWindow …). COM interfaces: `IGraphicsCaptureItemInterop`, `ICompositorDesktopInterop`, `ICompositorInterop`, `IDirect3DDxgiInterfaceAccess`, `IInitializeWithWindow`.

## 8. Dead / unused code
`Events/MonitoredWindowsEvents`, `Helpers/OperatingSystemHelper` + `Enums/OperatingSystemType`, `Entities/HexColor`, `Extensions/WindowExtensions/CommonExtensions`, `Structs/RECT` (RECT used by GetWindowRect—verify), tray controller (commented out), Windows.Storage/AppRecording/UI.Xaml usings in picker, `src/.vs` and `src/.idea` committed. Verify before deleting.

## 9. Project-level facts
- No tests, CI, installer (solution has empty "Installer" folder), logging (only `Console.WriteLine`).
- README (root + App/README.md) is user-facing (OBS setup).
