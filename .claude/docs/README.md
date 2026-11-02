# Docs index

Source of truth for agents. Update when code changes.

| Doc | Content |
|-----|---------|
| [architecture-map.md](architecture-map.md) | Projects, components, runtime flows, threading, DI, interop |
| [known-issues.md](known-issues.md) | Bugs, fragilities, resilience gaps, ordered by priority |
| [decisions.md](decisions.md) | Design decisions and rationale (append-only log) |
| [release-and-distribution.md](release-and-distribution.md) | CI, tag-driven release, winget/choco packaging, auto-update status |
| [web-ui.md](web-ui.md) | React web UI (`src/Web`), served by the app; focus-bridge protocol, build, limits |
| [profiles.md](profiles.md) | Saved window profiles: rule model, storage, service, privacy |
| [e2e-testing.md](e2e-testing.md) | UI-Automation e2e script (`.claude/scripts/e2e-focus.ps1`) for focus/fallback behaviour, gotchas |

## Maintenance rule
Each feature/change: check these docs, edit or add. New topic -> new file + row here. Keep `known-issues.md` current: remove items when fixed (note in `decisions.md` if the fix changed design).
