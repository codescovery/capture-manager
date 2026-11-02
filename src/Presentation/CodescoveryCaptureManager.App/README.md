# CODESCOVERY
## CodescoveryCaptureManager

A simple app that will help streamers to select  which windows they want to share with their viewers. 
The application will capture the user selected windows and display the one in focus. 
## Features

- Select/remove the desired windows to be captured by application
- Change CodescoveryCaptureManager window title
- Move CodescoveryCaptureManager to a VirtualDesktop if enabled;

## Installation Pre Requisites
- None (self-contained)

## Project NugetPackages Dependecies
- [SharpDX.D3DCompiler]
- [SharpDX.Direct3D11] 
- [VirtualDesktop] 
- [VirtualDesktop.WPF] 

## Installation

Download the latest release from GitHub, winget (`winget install Codescovery.CaptureManager`) or Chocolatey (`choco install codescovery-capture-manager`) once published.
Run CodescoveryCaptureManager-win-Setup.exe from the latest release (self-contained, no .NET install needed; updates itself). A portable zip is available as fallback without auto-update.

## Displaying in OBS
After selecting the windows. You should go to OBS and and a new WindowCaptureSource.
 - Select your the CodescoveryCaptureManager window
 - In Capture Method select (Windows Graphics Capture(Windows 10 1903 and up))
   -OBS:This step is important, without it OBS wont be able to display whats is being captured by CodescoveryCaptureManager.





## Development

Want to contribute? Great!

Feel free to suggest ideas, as soon as possible we will take a look check the availability to develop it.
Also you are all free to create a new branch and  create a pull request.

#### Debug from source code

You should start debugging from Presentation\CodescoveryCaptureManagerApp

## License

MIT



