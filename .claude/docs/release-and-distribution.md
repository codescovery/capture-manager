# Release & distribution

- **CI** `.github/workflows/ci.yml`: build on push to main + PRs (windows-latest, .NET 10 SDK).
- **CD** `.github/workflows/release.yml`: on tag `vX.Y.Z`:
  1. `dotnet publish` win-x64, **self-contained, single file** (`PublishSingleFile`, `IncludeNativeLibrariesForSelfExtract`, compression). ~79 MB exe, no .NET install needed. Verified locally: exe starts.
  2. `vpk download github` (previous release, for delta packages; allowed to fail on first release).
  3. `vpk pack` (Velopack, tool pinned `VPK_VERSION`) -> `CodescoveryCaptureManager-win-Setup.exe`, `-win-Portable.zip` (fallback, no auto-update), full/delta `.nupkg`, `releases.win.json` in `Releases/`.
  4. `vpk upload github --publish` -> GitHub Release for the tag.
  5. Optional Chocolatey push (secret `CHOCO_API_KEY`): installs the Setup.exe silently (`--silent`), checksum filled by workflow.
  6. Optional winget update (secret `WINGET_TOKEN`, `vedantmgoyal9/winget-releaser`); first version submitted manually (`packaging/winget/README.md`).
- Release a version: `git tag vX.Y.Z && git push origin vX.Y.Z`.
- **Auto-update** (Velopack, open source MIT): `Program.Main` runs `VelopackApp.Build().Run()` first (handles install/update hooks); `App.xaml` is a `Page` (not `ApplicationDefinition`) so our `Main` is the entry. After the window shows, `Updates/UpdateService.StageUpdateAsync` checks `GithubSource` (repo releases), downloads, and applies on next app exit (`WaitExitThenApplyUpdates`; never interrupts a stream). Only works when installed via Setup.exe (`IsInstalled`); the portable zip and dev runs skip silently. Errors are logged to console and swallowed.
- Updating package-manager installs: the app updates itself; winget/choco versions lag until their next publish, harmless.
- `global.json` pins SDK 10.0.401 temporarily because the .NET 11 RC SDK install needs a reboot; remove it when retargeting to net11 (and update ci/release `dotnet-version`).
- Untested: the workflows, `vpk pack`, Setup install, actual update flow, choco/winget publishing (never run on GitHub).
