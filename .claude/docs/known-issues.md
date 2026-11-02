# Known issues & resilience gaps

Found by static reading (not reproduced). Highest priority first. Remove when fixed.
Fixed 2026-11-02 (see decisions.md): monitoring loop try/catch + observed task, closed-window pruning, frame callback dispose safety, per-window enumeration guard, double dispose.

## Resilience
1. **Restarted apps not re-matched**: dictionary keyed by HWND; a closed window is now pruned (`MonitoredWindowClosed`) but a relaunched app has a new HWND and must be re-picked. Consider matching by process name + title.
2. **Minimized windows**: Graphics Capture yields no/frozen frames for minimized windows; no detection or fallback.
3. **Capture stop shows empty surface**: no placeholder/status shown to user when capture stops (closed window, error).
4. **Elevated windows**: app runs `asInvoker`; elevated targets can't be captured, no message.
5. **No real logging** (only `Console.WriteLine`); no global crash handling (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`).
6. Dictionary is mutated from monitoring tick and picker, both on UI thread; keep it that way or add locking.

## Correctness / design
8. Picker `Apply` toggles by *click parity*; calls `StopCapture` + `FocusFirstMonitoringWindow` per item.
9. After selecting windows, `FocusFirstMonitoringWindow` shows first dictionary entry, not the focused one.
10. Picker thumbnails use `CopyFromScreen` → captures whatever covers the window (overlaps, minimized, other virtual desktops). Branch `feature/previewSelectWindow` targets this; better: one Graphics Capture frame or DWM thumbnail.
11. 10 ms polling of foreground window; could use `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`.
12. Window names: `Substring(0, max)` + `"..."` always appended, even when short.
13. Hardcoded 3-per-row grid; all UI built in code-behind (no MVVM); picker has many unused usings.
15. `src/.vs`, `src/.idea`, `*.csproj.user` tracked in git (check .gitignore).
16. No tests; auto-update untested (Velopack wired but untested end to end: see release-and-distribution.md).
