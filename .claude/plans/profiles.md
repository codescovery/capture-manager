# Plan: capture profiles (saved sets of windows)

Status: phases 1-2 implemented (see docs/profiles.md); phases 3-4 pending.

## Goal
User saves a named set of windows ("Standup": Notepad + Visual Studio on `Foo.sln` + Teams chat) and re-applies it in one click, also after restarting apps.

## Core problem: window identity
HWNDs die when an app restarts, so a profile cannot store windows, it stores **rules**.

```
Profile { Id, Name, Rules[], FallbackMode, FallbackRuleIndex? }
WindowRule { ProcessName, TitleMatch (Any | Contains | Exact | Regex), TitlePattern, AutoAttach }
```

- Default rule built from a picked window: `ProcessName` exact + `Contains` of a user-editable title fragment (VS title `Foo - Microsoft Visual Studio` → fragment `Foo`; Notepad++ titles carry `*` and file names → suggest a shorter fragment). Regex is an advanced option.
- Resolution = enumerate with `IWindowsProcessServices.GetOpenedWindows`, keep windows matching any rule. A rule with no live window shows as "waiting".

## Architecture (Domain, no WPF)
- `CaptureProfile`, `WindowRule` (records) + `WindowRuleMatcher` (pure, unit-testable).
- `IProfileStore` → `JsonProfileStore`: `%AppData%\CodescoveryCaptureManager\profiles.json`, `System.Text.Json`, atomic write (temp + move), tolerate corrupt/missing file (start empty, keep a `.bak`).
- `IProfileService`: `Profiles`, `Active`, `Apply(profile)`, `SaveCurrentAs(name)`, `Delete`, events. `Apply` diffs resolved windows against the monitoring service (`Add/RemoveMonitoringWindow`), then `StopCapture` + `SyncWithForegroundWindow` (never shows an unfocused window).
- Live rebind: while a profile is active, rescan every ~2 s (not the 10 ms focus poll) and attach newly matching windows / drop vanished ones (already handled by `PruneClosedWindows`).
- App settings in the same folder (`settings.json`): last profile, fallback mode (fixes known-issue 17).

## UI
- Toolbar: **Profiles ▾** menu: profile list (✓ active), *Save current as…*, *Manage…*.
- Picker: footer *Save as profile…* (pre-fills rules from the selection, rule editor dialog), header shows the active profile; hand-editing the selection marks it "modified" with *Update profile* / *Save as new*.
- Manage dialog: rename, delete, edit rules (process, match mode, pattern, auto-attach), live "matches now: N" preview per rule.
- Status chip in the capture window: active profile + which rules are bound/waiting, so the user always sees what can be shared.
- Optional: `--profile "Name"` CLI arg and last-used profile on launch.

## Privacy rules (important)
Auto-attach can share a window the user never consciously picked (a new Notepad with sensitive text matches `Notepad` / Any title). So:
1. Default match is process + title fragment, never "Any title".
2. `AutoAttach` is per rule, default on only when the title fragment is non-empty; "Any title" rules default to off and show a warning.
3. Newly auto-attached windows are listed in the status chip and (setting) trigger a brief toast.

## Phases (one concern each)
1. Model + matcher + store + tests (new xUnit project; first tests in the repo).
2. Save from picker, apply from toolbar menu, persistence of last profile + fallback mode.
3. Live rebind loop + status chip.
4. Manage dialog, rule editor, import/export JSON, CLI arg.

## Docs to add when built
architecture-map (profile service, flow), new `profiles.md` (file format, matching, privacy), decisions, known-issues 17 removal, e2e script extension (apply profile → expected windows bound).

## Open questions
1. Match default: process + title fragment (recommended) vs process only.
2. Auto-attach windows that appear later: on by default for fragment rules (recommended) vs always ask.
3. Add an xUnit test project now (recommended, matcher is pure logic).
