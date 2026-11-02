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
   ├─ menu: title edit, virtual desktop move/close, open picker, min/max/close, drag, expander
   └─ OpenCapturePicker → new SelectCapturableWindows(...).Pick(this)

SelectCapturableWindows (picker, Windows/SelectCapturableWindows.xaml[.cs])
   ├─ IWindowsProcessServices.GetOpenedWindows(ignore self + main) → list of CapturableWindow
   ├─ builds Card grid in code (3 per row), screenshot thumbnail via GDI CopyFromScreen
   └─ Apply: toggles each clicked window in/out of monitoring service, StopCapture, FocusFirstMonitoringWindow

Domain/Services
   ├─ FocusBridgeService (internal, IFocusBridge): loopback WebSocket server publishing the focused picked window title to browser pages (see web-prototype.md)
   ├─ WindowsProcessServices (internal, IWindowsProcessServices): EnumWindows → visible + titled + not ignored process
   ├─ CapturableWindowsMonitoringService (internal, ICapturableWindowsMonitoringService): Dictionary<HWND,CapturableWindow>, poll loop
   ├─ WindowCaptureService (public): Composition visual tree (root → sprite w/ surface brush + drop shadow), owns D3D device, current CaptureService
   └─ CaptureService (internal): GraphicsCaptureItem + Direct3D11CaptureFramePool + SharpDX SwapChain1; copies frame → back buffer → Present; resizes on content size change
```

## 4. Runtime flows

**Startup**: `App()` → `Startup` (DI) + DispatcherQueueController → `App_OnStartup` resolves `CapturedWindows` → `Show()`. Ctor starts monitoring (nothing monitored yet, loop idles).

**Pick windows**: menu → `OpenCapturePicker` → picker enumerates windows (ignores `applicationframehost, shellexperiencehost, systemsettings, winstore.app, searchui, explorer`, hidden, untitled, self) → click card toggles in local `_capturedWindows` + checkbox → Apply: for each toggled window: StopCapture; if monitored remove else add; FocusFirstMonitoringWindow.

**Focus-follow loop** (`StartMonitoring`): every `CapturableWindowDefaultValues.RefreshRate` (10 ms) `Task.Delay` → `NativeMethods.GetActiveWindow()` (wraps `GetForegroundWindow`) → if foreground HWND is monitored → raise `MonitoredWindowFocused`. Each tick first prunes HWNDs failing `IsWindow` (raises `MonitoredWindowClosed`; UI stops capture if it was showing that window) and is wrapped in try/catch. UI handler skips if already capturing that HWND, else starts new capture (stops previous). Switching to a non-monitored window leaves last capture displayed.

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
