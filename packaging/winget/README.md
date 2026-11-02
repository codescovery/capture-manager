# Winget

Package id: `Codescovery.CaptureManager`. First version must be submitted manually to microsoft/winget-pkgs (e.g. `wingetcreate new <Setup.exe url>`, using `Codescovery.CaptureManager.installer.yaml` as reference). Later versions are automated by the `Winget update` step in `.github/workflows/release.yml` (needs secret `WINGET_TOKEN`, a PAT with `public_repo`).

Installer: Velopack `CodescoveryCaptureManager-win-Setup.exe`, type `exe`, silent switch `--silent`. App is self-contained: no runtime dependency.
