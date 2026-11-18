# Known issues & resilience gaps

Found by static reading (not reproduced). Highest priority first. Remove when fixed.
Fixed 2026-11-02 (see decisions.md): monitoring loop try/catch + observed task, closed-window pruning, frame callback dispose safety, per-window enumeration guard, double dispose.

## Resilience
1. **Restarted apps**: fixed for profiles (live rebind on focus, see decisions 2026-11-18). Windows picked by hand (no active profile) are still bound by HWND and must be re-picked after the app restarts.
2. **Minimized windows**: Graphics Capture yields no/frozen frames for minimized windows; no detection or fallback.
3. **Capture stop shows empty surface**: no placeholder/status shown to user when capture stops (closed window, error).
4. **Elevated windows**: app runs `asInvoker`; elevated targets can't be captured, no message.
5. **No real logging** (only `Console.WriteLine`); no global crash handling (`DispatcherUnhandledException`, `TaskScheduler.UnobservedTaskException`).
6. Dictionary is mutated from monitoring tick and picker, both on UI thread; keep it that way or add locking.

## Correctness / design
10. Picker thumbnails: `PrintWindow`, then one Graphics Capture frame as fallback. Minimized windows cannot be previewed; some overlay/protected windows may render black. Picker/new toolbar not yet verified visually by a human; no live refresh of the window list while open.
13. No MVVM: code-behind drives both windows (picker uses a small item view-model only).
15. `src/.vs`, `src/.idea`, `*.csproj.user` tracked in git (check .gitignore).
16. Few tests (xUnit only for profiles; UI via UIA scripts); auto-update untested (Velopack wired but untested end to end: see release-and-distribution.md).
17. Profiles: no manage/rename/edit dialog, no import/export. Fixed-window fallback is not persisted. Live rebind is lazy (a window is attached when it gets focus), so the selected-window count can lag until then.
