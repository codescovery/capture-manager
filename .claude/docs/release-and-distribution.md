# Release & distribution

- **CI** `.github/workflows/ci.yml`: build on push to main + PRs (windows-latest, .NET 10 SDK).
- **CD** `.github/workflows/release.yml`: on tag `vX.Y.Z`:
  1. `dotnet publish` win-x64, **self-contained, single file** (`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`). No .NET install needed. **No `EnableCompressionInSingleFile`**: see the table below.
  2. `vpk download github` (previous release, for delta packages; allowed to fail on first release).
  3. `vpk pack --runtime win-x64 --packAuthors Codescovery` (Velopack, tool pinned `VPK_VERSION`) -> `CodescoveryCaptureManager-win-Setup.exe`, `-win-Portable.zip`, full/delta `.nupkg`, `releases.win.json` in `Releases/`. Without `--runtime` vpk marks the package x86 (warning only).
  4. `vpk upload github --publish` -> GitHub Release for the tag.
  5. Optional Chocolatey push (secret `CHOCO_API_KEY`): installs the Setup.exe silently (`--silent`), checksum filled by workflow.
  6. Optional winget update (secret `WINGET_TOKEN`, `vedantmgoyal9/winget-releaser`); first version submitted manually (`packaging/winget/README.md`).
- Release a version: `git tag vX.Y.Z && git push origin vX.Y.Z`. The repo already has a 2021 release tagged `1.0.0` (no `v`, no Velopack files), so the next version must be above 1.0.0.
- **Auto-update** (Velopack, open source MIT): `Program.Main` runs `VelopackApp.Build()...Run()` first (handles install/update/uninstall hooks; the uninstall hook removes the "Start with Windows" Run entry); `App.xaml` is a `Page` (not `ApplicationDefinition`) so our `Main` is the entry. After the window shows, `Updates/UpdateService.StageUpdateAsync` checks `GithubSource` (repo releases), downloads, and applies when the app exits (`WaitExitThenApplyUpdates(update, silent: true, restart: false)`; never interrupts a stream, never reopens the app). Works for the Setup install **and** the portable zip (it ships `Update.exe`); a plain dev build skips. The outcome is written to the log (logging.md).
- `CAPTURE_MANAGER_UPDATE_SOURCE` (folder or http(s) URL with `releases.win.json` + packages) replaces the GitHub source: lets an update be tested without publishing.
- Updating package-manager installs: the app updates itself; winget/choco versions lag until their next publish, harmless.
- `global.json` pins SDK 10.0.401 temporarily because the .NET 11 RC SDK install needs a reboot; remove it when retargeting to net11 (and update ci/release `dotnet-version`).

## Tested locally on 2026-11-18 (same publish and pack commands as the workflow, vpk 1.2.161)
- `Setup.exe --silent`: exit 0, installs to `%LocalAppData%\CodescoveryCaptureManager` (`current\`, `Update.exe`), desktop + Start menu shortcuts, uninstall entry under HKCU (`DisplayName` Codescovery Capture Manager, `QuietUninstallString` = `Update.exe --uninstall --silent`). The app is **not** started by a silent install.
- Installed app runs (version from the package, web viewer answers 200).
- Update: 1.9.0 -> 1.9.1 through a 190 KB delta, and 1.9.1 -> 1.9.3 skipping a version. Downloaded at start, applied after the process ends, uninstall entry version updated.
- Against the real GitHub releases (only the 2021 release exists): "No update available", no error.
- Portable zip: runs, and checks for updates too.
- `Update.exe --uninstall --silent`: folder, shortcuts and uninstall entry removed; the Run entry is removed by our hook. `%AppData%\CodescoveryCaptureManager` (profiles, settings, logs) is left in place.
- `choco pack` of `packaging/chocolatey` with version and hash filled in: package built. `winget validate` of `packaging/winget` (three files): passes.

Single-file compression, measured (working set a few seconds after a hidden start):

| Build | exe on disk | Setup.exe | Memory |
|-------|-------------|-----------|--------|
| Dev build (not single file) | - | - | 235 MB |
| Single file, compressed | 80 MB | 84.8 MB | 367 MB |
| Single file, not compressed (current) | 191 MB | 86.9 MB | 200 to 221 MB |

Compression only saved disk space (the Velopack package is compressed anyway) and cost about 150 MB of memory.

Found and fixed by the test: the app reopened itself (window visible) after quitting when an update was pending; package marked x86; uninstall left the Run entry; publisher shown as the package id; winget template was one file and failed validation.

## Not tested
- The GitHub workflow itself (never run: needs a pushed tag, which publishes a release), `vpk upload github`, `vpk download github` against a real Velopack release.
- Chocolatey: `choco install` (needs the published download URL and an elevated shell), push, community moderation. There is no `chocolateyuninstall.ps1`; uninstall relies on Chocolatey's automatic uninstaller finding the HKCU entry.
- winget: a real `winget install` (local manifests need an admin setting), the first submission, the `winget-releaser` step.
- Installer UI (non-silent), SmartScreen / antivirus reaction to the unsigned Setup.exe (no code signing), install on a clean machine or another Windows user, update while the app is streaming, update when the app is closed by Windows shutdown.
- `Setup.exe --silent -- <app args>` crashes inside Velopack's setup (vpk 1.2.161, `EXE_ARGS` panic, process hangs): do not pass app arguments to the installer.
