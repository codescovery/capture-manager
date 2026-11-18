# Winget

Package id: `Codescovery.CaptureManager`. First version must be submitted manually to microsoft/winget-pkgs (e.g. `wingetcreate new <Setup.exe url>`, using the three manifest files here as reference: version, installer, `locale.en-US`; replace version, URL and hash, then check with `winget validate --manifest <folder>`). Later versions are automated by the `Winget update` step in `.github/workflows/release.yml` (needs secret `WINGET_TOKEN`, a PAT with `public_repo`).

Installer: Velopack `CodescoveryCaptureManager-win-Setup.exe`, type `exe`, per-user (`Scope: user`), silent switch `--silent`, uninstall entry key `CodescoveryCaptureManager` (`ProductCode`). App is self-contained: no runtime dependency.
