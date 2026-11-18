# Capture Manager

Share one window instead of your whole screen, and still switch between apps.

You pick the windows you are willing to show. Capture Manager displays, inside its own window, only the picked window that currently has focus. In Teams, Meet, OBS or any other tool you share the Capture Manager window: whatever you did not pick never reaches your viewers.

By [Codescovery](https://github.com/codescovery). Windows only.

## Features

- **Pick windows** from a list with live thumbnails; change the selection at any time.
- **Follows your focus**: the shown window switches as you switch apps. When you focus something you did not pick, viewers see a message, the last picked window, or one fixed window (your choice).
- **Pause** (button, tray menu or a global hotkey, `Ctrl+Alt+P` by default): hides everything at once until you resume.
- **Profiles**: save a set of windows as rules (program + title) and bring it back later. A program you restart is picked up again by itself.
- **Web viewer**: the shown window is also streamed to a page at `http://127.0.0.1:47800/` on the same computer, so you can share a browser tab instead of the app window.
- **Runs in the tray**: closing the window keeps capture and the web viewer going. Optional start with Windows.
- **Clean view** (`Ctrl+H`): hides the app's own bars so viewers see only the captured window.
- Custom title for the window in the share picker, optional move to its own virtual desktop.
- Updates itself when installed with the setup program.

## Requirements

- Windows 10 version 2004 or later, 64-bit. Windows 11 24H2 or later captures at the display's full refresh rate.
- Nothing else to install: the app ships with its own .NET runtime.
- Web viewer: a recent Chrome or Edge.

## Install

Download `CodescoveryCaptureManager-win-Setup.exe` from the [latest release](https://github.com/codescovery/capture-manager/releases) and run it. It installs for the current user, without administrator rights.

`CodescoveryCaptureManager-win-Portable.zip` in the same release runs without installing.

Packages for winget (`Codescovery.CaptureManager`) and Chocolatey (`codescovery-capture-manager`) are prepared in `packaging/` but not published yet.

## Use

1. Start Capture Manager and choose **Windows** to pick what you want to expose.
2. In your meeting or streaming tool, share the window named **Captured Window** (the name can be changed in Settings).
3. Work as usual. The picked window you focus is what viewers see.

### OBS

Add a **Window Capture** source, select the Capture Manager window, and set **Capture Method** to **Windows Graphics Capture** (Windows 10 1903 and up). With the other methods OBS shows an empty window.

### Web viewer

Open `http://127.0.0.1:47800/` in Chrome or Edge (use `127.0.0.1`, not `localhost`) and share that tab. **Output only** leaves just the picture in the tab. The address answers on this computer only.

## Privacy

- Only windows you picked are ever captured, and only while they have focus (or while you chose to keep one visible).
- The web viewer listens on the loopback address; nothing is sent to another machine.
- The log file (Settings > About > Open logs folder) records program names and window handles, never window titles.

## Problems

Settings > About > **Report an issue** opens a GitHub issue pre-filled with your app and Windows version. Attach the log if it helps.

## Build from source

Needs the .NET 10 SDK and Node 22 (the build compiles the web viewer).

```bash
dotnet build src/Codescovery.CaptureManager.sln
dotnet test  src/Codescovery.CaptureManager.sln
dotnet run --project src/Presentation/CodescoveryCaptureManager.App
```

`-p:SkipWebBuild=true` builds without Node; the desktop app works, the web viewer page is then not served.

Suggestions and pull requests are welcome.

## License

[MIT](LICENSE)
