# Logging and crash handling

## Where
- Files: `<data dir>\logs\capture-manager.log`, data dir = `%AppData%\CodescoveryCaptureManager` (or `CAPTURE_MANAGER_DATA_DIR`), same root as profiles/settings.
- Rotation by size: 1 MB per file, 5 files (`capture-manager.log`, then `capture-manager.1.log` newest .. `.4.log` oldest). Worst case about 5 MB.
- User reaches it from Settings > About > **Open logs folder** (`AppInfo.OpenLogsFolder`). The pre-filled GitHub issue only tells the user to attach the file: the path is not put in the URL (it holds the Windows user name).

## Code
- `Domain/Diagnostics/RollingFileLog`: the writer. Opens the file per entry (`FileShare.ReadWrite`, so an editor can keep it open), lock-guarded, never throws (IO failures are dropped). A line equal to the previous one is counted, not written; the count is flushed as `Previous entry repeated N more time(s).` when a different entry arrives or on dispose. This keeps a per-frame failure (60/s) from flooding the file. Clock is injectable for tests.
- `Domain/Diagnostics/AppLog`: static facade (`Info`, `Warning`, `Error`, `Fatal`). No-op until `AppLog.Start()`, so tests and tools never write files. Static (not `ILogger` through DI) because many callers are static helpers or objects created with `new` (`WindowCaptureService`, `H264Encoder`).
- `App/Helpers/CrashReporter`: `Register()` hooks `AppDomain.UnhandledException` (FATAL) and `TaskScheduler.UnobservedTaskException` (ERROR, marked observed). `ReportFatal` is called from `App.OnDispatcherUnhandledException`: logs FATAL, shows one message box with the log path, then the app shuts down with exit code 1 (UI-thread failures are not swallowed: the capture window could be left showing the wrong thing).
- `Program.Main`: `AppLog.Start()` after the single-instance check (the second instance never writes), `Started.` line with version/OS/runtime, `Exited with code N.` at the end.

## Conventions
- Level: `Error(message, exception)` writes the full exception (stack). `Warning` is for expected, recoverable things (inaccessible process, thumbnail failed, viewer socket dropped) and carries only `ex.Message`. `Info` for lifecycle: start/exit, monitoring start (hook or fallback), window picked/unpicked/released, window shown, profile applied (counts only), bridge listening, viewer connected/disconnected, stream start.
- **Privacy: never log window titles or profile names.** Identify a window as `<ProcessName> 0x<handle>`. File paths of the app's own data are fine.
- Do not use `Console.WriteLine`: the app is a `WinExe`, the output goes nowhere.

## Tests
`Tests/.../Diagnostics/RollingFileLogTests` (format, exception indent, repeat collapse, rotation, file limit, unwritable directory, `AppLog` before start).

## Not verified
- The three crash handlers firing in a real crash (no way to trigger one without a test hook in the app); only the normal path was run: start, bridge and monitoring lines written to an isolated data dir.
- The message box when the failure happens while the window is hidden in the tray.
