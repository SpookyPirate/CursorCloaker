<#
  readme-screenshots.ps1 - capture the README images into docs/images/.

  Shows the real UI over a plain dark backdrop window, so nothing from the desktop (other apps,
  window titles) ends up in a published image:
    docs/images/tray-menu.png          tray menu with the "Cloak style" submenu open
    docs/images/excluded-programs.png  exclusions window with a few running programs excluded
    docs/images/choose-program.png     running-programs picker, filtered by a search
    docs/images/icon.png               the app icon, 128 px (from the SVG)

  The example exclusions and the search must match programs running on this PC; adjust
  -Excluded and -Search when re-shooting elsewhere. Missing programs fall back to a placeholder
  icon, which looks wrong in a README.

  Usage (from the repo root, after dotnet build):
    powershell -ExecutionPolicy Bypass -File scripts\readme-screenshots.ps1
  It takes over the screen for about ten seconds.
#>
param(
    [string[]]$Excluded = @('discord', 'steam', 'parsecd', 'streamdeck'),
    [string]$Search = 'steam'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'src\CursorCloaker\bin\Debug\net48\CursorCloaker.exe'
if (-not (Test-Path $exe)) { throw "Build first: $exe not found" }
$out = Join-Path $root 'docs\images'
New-Item -ItemType Directory -Force $out | Out-Null

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @'
using System; using System.Drawing; using System.Runtime.InteropServices;
public static class Shot {
  [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
  [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT rect, int size);
  struct RECT { public int Left, Top, Right, Bottom; }
  public static Rectangle Frame(IntPtr window) { RECT f; DwmGetWindowAttribute(window, 9, out f, 16); return new Rectangle(f.Left, f.Top, f.Right - f.Left, f.Bottom - f.Top); }
  public static void Save(Rectangle area, string path) {
    using (var b = new Bitmap(area.Width, area.Height)) { using (var g = Graphics.FromImage(b)) g.CopyFromScreen(area.Location, Point.Empty, area.Size); b.Save(path); }
  }
}
'@
[void][Shot]::SetProcessDpiAwarenessContext([IntPtr]::new(-4))
[Windows.Forms.Application]::EnableVisualStyles()

function Pump([int]$ms) { $end = [DateTime]::Now.AddMilliseconds($ms); while ([DateTime]::Now -lt $end) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 15 } }
function Expand([Drawing.Rectangle]$r, [int]$margin) { $r.Inflate($margin, $margin); $r }

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$settingsType = $assembly.GetType('CursorCloaker.Core.CloakSettings')
$settings = [Activator]::CreateInstance($settingsType, $true)
foreach ($name in $Excluded) { [void]$settings.ExcludedProcesses.Add($name) }
# PowerShell wraps values in PSObject; reflection needs the raw objects in a typed array.
function New-Instance([string]$typeName, [object[]]$values) {
    $raw = [object[]]::new($values.Count)
    for ($i = 0; $i -lt $values.Count; $i++) { $raw[$i] = $values[$i].psobject.BaseObject }
    $assembly.GetType($typeName).GetConstructors($flags)[0].Invoke($raw)
}
function Field($object, $name) { $object.GetType().GetField($name, $flags).GetValue($object) }

# Backdrop: one step darker than the app's window background, so window edges and rounded
# corners stay visible against it.
$screen = [Windows.Forms.Screen]::PrimaryScreen.Bounds
$backdrop = New-Object Windows.Forms.Form
$backdrop.FormBorderStyle = 'None'; $backdrop.StartPosition = 'Manual'; $backdrop.Bounds = $screen
$backdrop.BackColor = [Drawing.Color]::FromArgb(0x0D, 0x0F, 0x14); $backdrop.ShowInTaskbar = $false; $backdrop.TopMost = $true
$backdrop.Show(); Pump 300
$margin = [int](40 * $backdrop.DeviceDpi / 96)

try {
    # Tray menu with a submenu open.
    $trayMenu = New-Instance 'CursorCloaker.Ui.TrayMenu' @($assembly.GetType('CursorCloaker.TrayContext').GetMethod('VersionText', $flags).Invoke($null, @()), $settings)
    $menu = $trayMenu.Menu
    $menu.Show((New-Object Drawing.Point ($screen.X + 400), ($screen.Y + 300))); Pump 300
    $style = $menu.Items | Where-Object { $_.Text -eq 'Cloak style' }
    [void]$style.Select(); $style.ShowDropDown(); Pump 500
    [Shot]::Save((Expand ([Drawing.Rectangle]::Union($menu.Bounds, $style.DropDown.Bounds)) $margin), (Join-Path $out 'tray-menu.png'))
    $menu.Close(); $menu.Dispose()

    # Exclusions window.
    $window = New-Instance 'CursorCloaker.Ui.ExclusionsWindow' @(,$settings)
    $window.TopMost = $true; $window.Show(); $window.Activate(); Pump 400
    $list = Field $window 'list'; $list.SelectedIndex = 1; $list.Focus(); Pump 400
    [Shot]::Save((Expand ([Shot]::Frame($window.Handle)) $margin), (Join-Path $out 'excluded-programs.png'))
    $window.Close(); $window.Dispose()

    # Running-programs picker, filtered.
    $picker = New-Instance 'CursorCloaker.Ui.ProgramPickerWindow' @([Enum]::Parse($assembly.GetType('CursorCloaker.Ui.ProgramSource'), 'RunningPrograms'), $settings.ExcludedProcesses)
    $picker.StartPosition = 'CenterScreen'; $picker.TopMost = $true; $picker.Show(); $picker.Activate(); Pump 2500
    (Field $picker 'search').Text = $Search; Pump 300
    $pickerList = Field $picker 'list'
    for ($i = 0; $i -lt $pickerList.Items.Count; $i++) { if (-not $pickerList.Items[$i].IsAlreadyExcluded) { $pickerList.SelectedIndex = $i; break } }
    Pump 400
    [Shot]::Save((Expand ([Shot]::Frame($picker.Handle)) $margin), (Join-Path $out 'choose-program.png'))
    $picker.Close(); $picker.Dispose()
}
finally {
    $backdrop.Close(); $backdrop.Dispose()
}

& magick -background none -density 192 (Join-Path $root 'graphics\CursorCloaker-icon1.svg') -resize 128x128 (Join-Path $out 'icon.png')
"Saved to $out"
