# E2E for profiles: save from the picker, see it in the menu, persist to disk, auto-apply after restart.
# Uses a private data dir (CAPTURE_MANAGER_DATA_DIR) so the user's real profiles are untouched.
# Run: powershell -File .claude\scripts\e2e-profiles.ps1   (Debug build, no other app instance running)
. "$PSScriptRoot\e2e-lib.ps1"
$dataDir = Join-Path $env:TEMP ("ccm-e2e-" + [guid]::NewGuid().ToString('N'))
$alpha = Join-Path $env:TEMP "alpha-profile-test.txt"; "alpha" | Set-Content $alpha
Start-Process notepad $alpha; Start-Sleep 3
$ok = $true
function Check([string]$what, [bool]$cond) { if (-not $cond) { $script:ok = $false }; "{0,-6} {1}" -f $(if ($cond) { 'PASS' } else { 'FAIL' }), $what }

try {
  # --- run 1: create a profile from the picker ---
  $app = Start-TestApp $dataDir
  $main = $app.Main
  $picker = Open-Picker-And-Select $main 'alpha-profile-test'
  Invoke-El (ByName $picker 'Save as profile...')
  Start-Sleep 2
  $dialog = Owned-Windows $main | ? { $_.Current.Name -eq 'Save profile' } | Select-Object -First 1
  Check 'save dialog opens' ($null -ne $dialog)
  (ById $dialog 'ProfileName').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('E2E Profile')
  Invoke-El (ByName $dialog 'Save')
  Start-Sleep 1
  $profilesJson = Join-Path $dataDir 'profiles.json'
  Check 'profiles.json written' (Test-Path $profilesJson)
  if (Test-Path $profilesJson) {
    $json = Get-Content $profilesJson -Raw
    Check 'rule has process + title' (($json -match 'notepad') -and ($json -match 'alpha-profile-test'))
  }
  Invoke-El (ById $picker 'ApplyButton')
  Start-Sleep 2

  # --- profile menu lists it, applying works ---
  Invoke-El (ById $main 'ProfilesButton')
  Start-Sleep 1
  Check 'profile appears in menu' ($null -ne (Get-Menu-Items 'E2E Profile' | Select-Object -First 1))
  Stop-Process -Id $app.Process.Id -Force
  Start-Sleep 2

  # --- run 2: save current via menu is covered by unit tests; here check persistence + auto-apply on restart ---
  # (picker-saved profile is not active, so apply it from the menu, restart, expect it restored as active)
  $app = Start-TestApp $dataDir
  $main = $app.Main
  Invoke-El (ById $main 'ProfilesButton'); Start-Sleep 1
  $item = Get-Menu-Items 'E2E Profile' | Select-Object -First 1
  Invoke-El $item; Start-Sleep 2
  $notepad = Get-Process notepad | ? { $_.MainWindowTitle -like '*alpha-profile-test*' } | Select-Object -First 1
  Focus-Window $notepad.MainWindowHandle
  Check 'applied profile captures focused Notepad' ((Overlay-State $main) -eq 'CAPTURING')
  Stop-Process -Id $app.Process.Id -Force
  Start-Sleep 2

  $app = Start-TestApp $dataDir
  $main = $app.Main
  Start-Sleep 2
  Focus-Window $notepad.MainWindowHandle
  Check 'last profile auto-applied after restart' ((Overlay-State $main) -eq 'CAPTURING')
  $label = (ById $main 'ProfileLabel')
  Check 'toolbar shows active profile name' ($null -ne (ByName $main 'E2E Profile'))
}
finally {
  Get-Process CodescoveryCaptureManager.App -ErrorAction SilentlyContinue | Stop-Process -Force
  Get-Process notepad -ErrorAction SilentlyContinue | ? { $_.MainWindowTitle -like '*alpha-profile-test*' } | Stop-Process -Force
  Remove-Item $alpha -ErrorAction SilentlyContinue
  Remove-Item $dataDir -Recurse -ErrorAction SilentlyContinue
  Remove-Item Env:CAPTURE_MANAGER_DATA_DIR -ErrorAction SilentlyContinue
}
if ($ok) { 'ALL PASS' } else { 'SOME FAILED'; exit 1 }
