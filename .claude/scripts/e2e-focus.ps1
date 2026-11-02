# E2E check of focus-follow + fallback modes through UI Automation (no human). Build Debug first, close other app instances, run: powershell -File .claude\scripts\e2e-focus.ps1
# Needs: Edge running (used as the 'unpicked foreign window'). Opens Notepad + the app, picks Notepad, switches focus, prints overlay state per step, cleans up.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System; using System.Text; using System.Runtime.InteropServices;
public static class Native {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public static void Click(int x, int y) { SetCursorPos(x, y); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero); }
  public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
"@
$script:root = [System.Windows.Automation.AutomationElement]::RootElement
function Find-Top([string]$namePart, [int]$pid2 = 0) {
  $kids = $script:root.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
  foreach ($k in $kids) {
    if ($pid2 -and $k.Current.ProcessId -ne $pid2) { continue }
    if ($k.Current.Name -like "*$namePart*") { return $k }
  }
  return $null
}
function Find-ByName($parent, [string]$name) {
  $c = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
  return $parent.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $c)
}
function Fg { [Native]::Title([Native]::GetForegroundWindow()) }


[void][Native]::SetProcessDPIAware()
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class Kb { [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra); }
"@
$UIA = [System.Windows.Automation.AutomationElement]
$Tree = [System.Windows.Automation.TreeScope]
function ById($p, $id) { $p.FindFirst($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::AutomationIdProperty, $id))) }
function ByName($p, $n) { $p.FindFirst($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty, $n))) }
function Focus-Window([IntPtr]$h) {
  [Kb]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [Kb]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
  [void][Native]::ShowWindow($h, 9); [void][Native]::SetForegroundWindow($h); Start-Sleep -Milliseconds 800
}

$alpha = Join-Path $env:TEMP "alpha-test.txt"; "alpha" | Set-Content $alpha
Start-Process notepad $alpha; Start-Sleep 3
$exe = Join-Path $PSScriptRoot "..\..\src\Presentation\CodescoveryCaptureManager.App\bin\Debug\net10.0-windows10.0.19041.0\CodescoveryCaptureManager.App.exe"
$app = Start-Process $exe -PassThru
$appPid = $app.Id
$main = $null
for ($i = 0; $i -lt 40 -and -not $main; $i++) { Start-Sleep -Milliseconds 500; $main = Find-Top "Captured Window" $appPid }
if (-not $main) { "app window did not appear (exited=$($app.HasExited))"; exit 1 }
Start-Sleep 1
function Overlay-State {
  $m = Find-Top "Captured Window" $appPid
  $t = $m.FindAll($Tree::Descendants, [System.Windows.Automation.Condition]::TrueCondition) | ? { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Text' } | % { $_.Current.Name }
  if ($t -contains 'No selected window is focused') { 'BLANK + NoFocus message' } elseif ($t -contains 'No windows selected') { 'EmptyState' } else { 'CAPTURING' }
}

# pick notepad
(ByName $main 'Choose windows' | Out-Null)
$btn = $main.FindAll($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::NameProperty,'Choose windows'))) | ? { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' } | Select-Object -First 1
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 4
$winCond = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)
$picker = $main.FindAll($Tree::Descendants, $winCond) | ? { $_.Current.Name -like '*Choose windows to share*' } | Select-Object -First 1
(ById $picker 'SearchBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('alpha-test'); Start-Sleep 1
$r = $picker.Current.BoundingRectangle
[Native]::Click([int]($r.X + $r.Width/2), [int]($r.Y + 192)); Start-Sleep 1
"picker summary: $((ById $picker 'SelectionSummary').Current.Name)"
(ById $picker 'ApplyButton').GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 2

"0) right after Apply (own app focused, Notepad NOT focused) -> $(Overlay-State)   [must NOT be CAPTURING]"
$notepad = Get-Process notepad | ? { $_.MainWindowTitle -like '*alpha-test*' } | Select-Object -First 1
$edge = Get-Process msedge | ? { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
$mainHwnd = [IntPtr]$main.Current.NativeWindowHandle

# default mode: message
Focus-Window $edge.MainWindowHandle
"A) default mode, Edge focused            -> $(Overlay-State)"

# open settings menu -> fallback submenu -> 'Always show: alpha-test...'
Focus-Window $mainHwnd
$settings = ById $main 'SettingsButton'
$settings.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1
$rootKids = $script:root.FindAll($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty,[System.Windows.Automation.ControlType]::MenuItem)))
$sub = $rootKids | ? { $_.Current.Name -like 'When no selected window*' } | Select-Object -First 1
if (-not $sub) { "fallback submenu not found; menu items: " + (($rootKids | % { $_.Current.Name }) -join ' | '); exit 2 }
try { $sub.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand() } catch { "expand err: $_" }
Start-Sleep 1
$kids = $script:root.FindAll($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty,[System.Windows.Automation.ControlType]::MenuItem)))
"fallback items: " + (($kids | ? { $_.Current.Name -like 'Show a*' -or $_.Current.Name -like 'Keep*' -or $_.Current.Name -like 'Always show*' } | % { $_.Current.Name }) -join ' | ')
$always = $kids | ? { $_.Current.Name -like 'Always show: alpha-test*' } | Select-Object -First 1
$always.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1

Focus-Window $notepad.MainWindowHandle
Focus-Window $edge.MainWindowHandle
"B) fixed=Notepad, Edge focused           -> $(Overlay-State)"

# switch to keep-last, then message
$settings.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1
$kids = $script:root.FindAll($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty,[System.Windows.Automation.ControlType]::MenuItem)))
$sub = $kids | ? { $_.Current.Name -like 'When no selected window*' } | Select-Object -First 1
$sub.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand(); Start-Sleep 1
$kids = $script:root.FindAll($Tree::Descendants, (New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty,[System.Windows.Automation.ControlType]::MenuItem)))
($kids | ? { $_.Current.Name -eq 'Show a message' } | Select-Object -First 1).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); Start-Sleep 1
"C) back to message (Edge still focused?) fg='$(Fg)' -> $(Overlay-State)"

# cleanup
Stop-Process -Id $appPid -Force -ErrorAction SilentlyContinue
Get-Process notepad -ErrorAction SilentlyContinue | ? { $_.MainWindowTitle -like '*alpha-test*' } | Stop-Process -Force
Remove-Item $alpha -ErrorAction SilentlyContinue
"cleanup done"

