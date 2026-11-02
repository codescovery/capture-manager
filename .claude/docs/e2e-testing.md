# End-to-end testing (no test projects exist)

`.claude/scripts/e2e-focus.ps1` drives the real app through Windows UI Automation (UIA): opens Notepad + the app, opens the picker, selects Notepad by search, Apply, then switches the foreground window and reads which overlay the capture window shows.

Run (Debug build, no other app instance running, Edge open):

```bash
dotnet build src/Codescovery.CaptureManager.sln
powershell -File .claude/scripts/e2e-focus.ps1
```

Expected output: `0) … BLANK + NoFocus message` (nothing shown right after Apply), `A) … BLANK + NoFocus message`, `B) fixed=Notepad … CAPTURING`, `C) … BLANK + NoFocus message`.

## How it observes state
- Overlay text elements are in the UIA tree only while visible: `No selected window is focused` = blank view, `No windows selected` = nothing picked, neither = a window is being shown.
- It cannot see the composition visual itself (pixels); "CAPTURING" means "no overlay", inferred from `WindowCaptureService.IsCapturing()` driving `UpdateEmptyState()`.

## Gotchas learned
- The picker is an **owned** window: find it as a Window *descendant of the main window element*, not under the UIA root.
- Moving focus needs an Alt key tap before `SetForegroundWindow` (foreground lock).
- Make the script DPI aware (`SetProcessDPIAware`) before using UIA rectangles for real mouse clicks; the app may sit on a monitor with negative coordinates.
- A running app instance locks `bin\Debug` DLLs; build with `-p:OutDir=<other dir>` or close the app first. Stale instances caused a false "still captures unfocused windows" report once.
