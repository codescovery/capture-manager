$ErrorActionPreference = 'Stop'

$packageArgs = @{
  packageName    = $env:ChocolateyPackageName
  fileType       = 'exe'
  silentArgs     = '--silent'
  validExitCodes = @(0)
  url64bit       = 'https://github.com/codescovery/captureManager/releases/download/v__VERSION__/CodescoveryCaptureManager-win-Setup.exe'
  checksum64     = '__SHA256__'
  checksumType64 = 'sha256'
}

Install-ChocolateyPackage @packageArgs
