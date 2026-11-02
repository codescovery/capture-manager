# Shared helpers for the e2e scripts (dot-source). Drives the real app through UI Automation. See .claude/docs/e2e-testing.md.
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
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  public static void Click(int x, int y) { SetCursorPos(x, y); mouse_event(2,0,0,0,UIntPtr.Zero); mouse_event(4,0,0,0,UIntPtr.Zero); }
  public static string Title(IntPtr h) { var sb = new StringBuilder(256); GetWindowText(h, sb, 256); return sb.ToString(); }
}
"@
[void][Native]::SetProcessDPIAware()
$script:UIA = [System.Windows.Automation.AutomationElement]
$script:Tree = [System.Windows.Automation.TreeScope]
$script:root = $script:UIA::RootElement
$script:AppExe = Join-Path $PSScriptRoot "..\..\src\Presentation\CodescoveryCaptureManager.App\bin\Debug\net10.0-windows10.0.19041.0\CodescoveryCaptureManager.App.exe"

function Fg { [Native]::Title([Native]::GetForegroundWindow()) }
function Prop($p, $v) { New-Object System.Windows.Automation.PropertyCondition($p, $v) }
function ById($parent, [string]$id) { $parent.FindFirst($script:Tree::Descendants, (Prop $script:UIA::AutomationIdProperty $id)) }
function ByName($parent, [string]$name) { $parent.FindFirst($script:Tree::Descendants, (Prop $script:UIA::NameProperty $name)) }
function Invoke-El($el) { $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke() }
function Find-Top([string]$namePart, [int]$ownerPid = 0) {
  foreach ($k in $script:root.FindAll($script:Tree::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
    if ($ownerPid -and $k.Current.ProcessId -ne $ownerPid) { continue }
    if ($k.Current.Name -like "*$namePart*") { return $k }
  }
}
function Focus-Window([IntPtr]$h) {
  [Native]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero); [Native]::keybd_event(0x12, 0, 2, [UIntPtr]::Zero)
  [void][Native]::ShowWindow($h, 9); [void][Native]::SetForegroundWindow($h); Start-Sleep -Milliseconds 800
}
# Starts the app (optionally with a private data dir) and waits for its window. Returns @{Process; Main}.
function Start-TestApp([string]$dataDir = '') {
  if ($dataDir) { $env:CAPTURE_MANAGER_DATA_DIR = $dataDir }
  $proc = Start-Process $script:AppExe -PassThru
  $main = $null
  for ($i = 0; $i -lt 40 -and -not $main; $i++) { Start-Sleep -Milliseconds 500; $main = Find-Top "Captured Window" $proc.Id }
  if (-not $main) { throw "app window did not appear (exited=$($proc.HasExited))" }
  Start-Sleep 1
  @{ Process = $proc; Main = $main }
}
function Overlay-State($main) {
  $t = $main.FindAll($script:Tree::Descendants, [System.Windows.Automation.Condition]::TrueCondition) |
    ? { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Text' } | % { $_.Current.Name }
  if ($t -contains 'No selected window is focused') { 'BLANK + NoFocus message' } elseif ($t -contains 'No windows selected') { 'EmptyState' } else { 'CAPTURING' }
}
function Owned-Windows($main) {
  $main.FindAll($script:Tree::Descendants, (Prop $script:UIA::ControlTypeProperty ([System.Windows.Automation.ControlType]::Window)))
}
# Opens the picker, filters by text, clicks the first card. Returns the picker element.
function Open-Picker-And-Select($main, [string]$filter) {
  $btn = $main.FindAll($script:Tree::Descendants, (Prop $script:UIA::NameProperty 'Choose windows')) | ? { $_.Current.ControlType.ProgrammaticName -eq 'ControlType.Button' } | Select-Object -First 1
  Invoke-El $btn; Start-Sleep 4
  $picker = Owned-Windows $main | ? { $_.Current.Name -like '*Choose windows to share*' } | Select-Object -First 1
  (ById $picker 'SearchBox').GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($filter); Start-Sleep 1
  $r = $picker.Current.BoundingRectangle
  [Native]::Click([int]($r.X + $r.Width/2), [int]($r.Y + 192)); Start-Sleep 1
  $picker
}
function Get-Menu-Items([string]$likeName) {
  $script:root.FindAll($script:Tree::Descendants, (Prop $script:UIA::ControlTypeProperty ([System.Windows.Automation.ControlType]::MenuItem))) | ? { $_.Current.Name -like $likeName }
}
