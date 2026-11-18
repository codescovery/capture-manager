# Profiles

Saved sets of windows, re-applied in one click. Design rationale and later phases: [../plans/profiles.md](../plans/profiles.md). Implemented: phases 1-2 (model, store, matcher, save from picker/toolbar, apply, persistence, restore on start). Not yet: live rebind loop (phase 3), manage dialog/import-export/CLI arg (phase 4).

## Model (`Domain/Profiles`)
- `WindowRule(ProcessName, TitleMatch, TitlePattern, AutoAttach)`; `TitleMatch` = `Any | Contains | Exact | Regex`. Process name compare and title compare are case-insensitive. Invalid/slow regex (200 ms timeout) = no match, never throws.
- `CaptureProfile(Id, Name, Rules)`. `AppSettings(LastProfileId, FallbackMode)`.
- HWNDs are never stored (apps get new handles on restart). `CapturableWindow.ProcessName` carries the process name so matching does not touch the disposable `Process`; `WindowTitles.TitleOf` strips the `" (process.exe)"` suffix from `Name`.
- `WindowRuleFactory.FromWindow`: process + `Contains` title (leading `*`/`●` dirty markers stripped). Empty title → `Any` with auto-attach off.

## Persistence
`JsonProfileStore`: `%AppData%\CodescoveryCaptureManager\profiles.json` and `settings.json` (camelCase, enums as strings). Override directory with env var `CAPTURE_MANAGER_DATA_DIR` (used by e2e tests). Atomic write (temp + move). Missing file → empty; corrupt file → empty and the bad file is copied to `*.bak`.

Fallback mode is persisted for `Message`/`KeepLast` only; "Always show: <window>" is a live handle and resets to message on restart.

## Service (`ProfileService`, singleton)
- `Save(profile, makeActive)` upsert by Id; `Delete`; `ClearActive` (picker Apply = manual selection); `Apply(id)`: resolve rules against `GetOpenedWindows()` (own process windows excluded), replace the monitored set (remove stale, add matches), mark active, return `ProfileApplyResult(Bound, WaitingRules)`.
- Applying never shows an unfocused window: the UI follows it with `StopCapture` + `SyncWithForegroundWindow`.

## Privacy
- Rule editor forces `AutoAttach=false` for `Any` rules ("matches every window of this app" warning). AutoAttach is stored but only acted on once phase 3 (live rebind) exists.
- A profile can only ever expose windows its rules match, and the picker/profile menu is the only way windows join the monitored set.

## Verification
Unit tests in `src/Tests/CodescoveryCaptureManager.Domain.Tests`; UI flow in `.claude/scripts/e2e-profiles.ps1` (see [e2e-testing.md](e2e-testing.md)).
