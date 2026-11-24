<#
  preview-windows.ps1 - render CursorCloaker's windows and save screenshots, for visual review.

  Loads the built CursorCloaker.exe and opens, one at a time, with real data from this PC:
    preview\exclusions-empty.png       the exclusions window with nothing excluded
    preview\exclusions-filled.png      with sample exclusions (some running, some not), a row selected
    preview\exclusions-add-menu.png    with the Add program menu open
    preview\picker-running.png         the running-programs picker, a row selected
    preview\picker-windows.png         the open-windows picker
  Windows are shown topmost and captured from the screen. Each is open for about a second, so
  don't run it while someone is using the PC.

  Usage (from the repo root, after dotnet build):
    powershell -ExecutionPolicy Bypass -File scripts\preview-windows.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\CursorCloaker\bin\Debug\net48\CursorCloaker.exe'
if (-not (Test-Path $exe)) { throw "Build first: $exe not found" }
$out = Join-Path $root 'preview'
New-Item -ItemType Directory -Force $out | Out-Null

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @'
using System; using System.Drawing; using System.Runtime.InteropServices;
public static class Capture {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT rect, int size);
  struct RECT { public int Left, Top, Right, Bottom; }
  // On-screen capture of the window's visible frame. (PrintWindow with PW_RENDERFULLCONTENT was
  // tried and leaves owner-drawn regions stale on screen afterwards, so it isn't used.)
  public static void Window(IntPtr window, string path) {
    RECT frame; DwmGetWindowAttribute(window, 9, out frame, 16);   // visible frame, without the invisible resize borders
    Screen(new Rectangle(frame.Left, frame.Top, frame.Right - frame.Left, frame.Bottom - frame.Top), path);
  }
  public static void Screen(Rectangle area, string path) {
    using (var b = new Bitmap(area.Width, area.Height)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(area.Location, Point.Empty, area.Size); b.Save(path); }
  }
}
'@
[void][Capture]::SetProcessDpiAwarenessContext([IntPtr]::new(-4))
# As the app does in Program.Main; without it native controls (e.g. the search cue banner) fall back to old styles.
[Windows.Forms.Application]::EnableVisualStyles()

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$settingsType = $assembly.GetType('CursorCloaker.Core.CloakSettings')
function Pump([int]$ms) { $end = [DateTime]::Now.AddMilliseconds($ms); while ([DateTime]::Now -lt $end) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 15 } }
function New-Settings([string[]]$excluded) { $s = [Activator]::CreateInstance($settingsType, $true); foreach ($e in $excluded) { [void]$s.ExcludedProcesses.Add($e) }; $s }
function Field($object, $name) { $object.GetType().GetField($name, $flags).GetValue($object) }

# Exclusions window: empty, filled, add menu.
$windowType = $assembly.GetType('CursorCloaker.Ui.ExclusionsWindow')
$window = [Activator]::CreateInstance($windowType, $flags, $null, @(New-Settings @()), $null)
$window.TopMost = $true; $window.Show(); $window.Activate(); Pump 600
[Capture]::Window($window.Handle, (Join-Path $out 'exclusions-empty.png'))

# A running program (explorer), Steam if present, and one that isn't running.
$windowType.GetMethod('Refresh', $flags, $null, @($settingsType), $null).Invoke($window, @(New-Settings @('explorer', 'steam', 'retrobat', 'notarealgame')))
$list = Field $window 'list'; $list.SelectedIndex = 1; $list.Focus(); Pump 400
[Capture]::Window($window.Handle, (Join-Path $out 'exclusions-filled.png'))

$addButton = Field $window 'addButton'; $addMenu = Field $window 'addMenu'
$addMenu.Show($addButton, (New-Object Drawing.Point 0, ($addButton.Height + 6))); Pump 400
$bounds = $window.Bounds; $menuBounds = $addMenu.Bounds
$area = [Drawing.Rectangle]::Union($bounds, $menuBounds); $area.Inflate(8, 8)
[Capture]::Screen($area, (Join-Path $out 'exclusions-add-menu.png'))
$addMenu.Close(); $window.Close(); $window.Dispose()

# Pickers.
$pickerType = $assembly.GetType('CursorCloaker.Ui.ProgramPickerWindow'); $sourceType = $assembly.GetType('CursorCloaker.Ui.ProgramSource')
$excluded = New-Object 'System.Collections.Generic.List[string]'; $excluded.Add('explorer')
foreach ($source in 'RunningPrograms', 'OpenWindows') {
    $arguments = [object[]]::new(2); $arguments[0] = [Enum]::Parse($sourceType, $source); $arguments[1] = $excluded.psobject.BaseObject
    $picker = $pickerType.GetConstructors($flags)[0].Invoke($arguments)
    $picker.TopMost = $true; $picker.Show(); $picker.Activate(); Pump 2500
    $pickerList = Field $picker 'list'
    if ($source -eq 'RunningPrograms') {
        for ($i = 0; $i -lt $pickerList.Items.Count; $i++) { if (-not $pickerList.Items[$i].IsAlreadyExcluded) { $pickerList.SelectedIndex = $i; break } }
        Pump 300
    }
    $name = if ($source -eq 'RunningPrograms') { 'picker-running.png' } else { 'picker-windows.png' }
    [Capture]::Window($picker.Handle, (Join-Path $out $name))
    "$name : $($pickerList.Items.Count) entries"
    $picker.Close(); $picker.Dispose()
}
"Saved to $out"
