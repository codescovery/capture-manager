# CODESCOVERY
## CodescoveryCaptureManager

A simple app that will help streamers to select  which windows they want to share with their viewers. 
The application will capture the user selected windows and display the one in focus. 
## Features

- Select/remove the desired windows to be captured by application
- Change CodescoveryCaptureManager window title
- Move CodescoveryCaptureManager to a VirtualDesktop if enabled;

## Installation Pre Requisites
- .Net Core 3.1 Runtime(https://dotnet.microsoft.com/download/dotnet/3.1)

## Project NugetPackages Dependecies
- [SharpDX.D3DCompiler]
- [SharpDX.Direct3D11] 
- [VirtualDesktop] 
- [VirtualDesktop.WPF] 

## Installation

CodescoveryCaptureManager requires [.NetCore 3.1 Runtime](https://dotnet.microsoft.com/download/dotnet/3.1) to run.
Download the desired release and extract to anywhere you want.
Run CodescoveryCaptureManager.App.exe

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



