<#
  preview-menu.ps1 - render the real tray menu and save screenshots, for visual review.

  Loads the built CursorCloaker.exe, builds the actual TrayMenu with default settings, and saves:
    preview\menu.png          the menu with one row in its hover state (-Select)
    preview\menu-submenu.png  the same with the "Move to" submenu open
    preview\menu-hide-mode.png  with Cloak style set to Hide, where "Move to" is greyed out
  Every state the menu uses appears: checked and unchecked items, radio choices, submenus,
  separators, the version label.

  Usage (from the repo root, after dotnet build):
    powershell -ExecutionPolicy Bypass -File scripts\preview-menu.ps1 [-Select 2]

  Note: it opens a real menu on the primary screen for about two seconds.
#>
param(
    [int]$Select = 2,
    [switch]$Release
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configuration = if ($Release) { 'Release' } else { 'Debug' }
$exe = Join-Path $root "src\CursorCloaker\bin\$configuration\net48\CursorCloaker.exe"
if (-not (Test-Path $exe)) { throw "Build first: $exe not found" }
$out = Join-Path $root 'preview'
New-Item -ItemType Directory -Force $out | Out-Null

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PreviewDpi { [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value); }
'@
[void][PreviewDpi]::SetProcessDpiAwarenessContext([IntPtr]::new(-4))   # per-monitor v2, as the app
[Windows.Forms.Application]::EnableVisualStyles()

function Pump([int]$ms) { $end = [DateTime]::Now.AddMilliseconds($ms); while ([DateTime]::Now -lt $end) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 15 } }
function Save-Area([Drawing.Rectangle]$area, [string]$name) {
    $area.Inflate(24, 24)   # margin so the corners and shadow edge are visible
    $bitmap = New-Object Drawing.Bitmap $area.Width, $area.Height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($area.Location, [Drawing.Point]::Empty, $area.Size)
    $bitmap.Save((Join-Path $out $name), [Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
}

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$settings = [Activator]::CreateInstance($assembly.GetType('CursorCloaker.Core.CloakSettings'), $true)
$trayMenu = [Activator]::CreateInstance($assembly.GetType('CursorCloaker.Ui.TrayMenu'), $flags, $null, @('v0.0.0-preview', $settings), $null)
$menu = $trayMenu.Menu

$menu.Show((New-Object Drawing.Point 200, 200))
if ($Select -ge 0 -and $Select -lt $menu.Items.Count) { [void]$menu.Items[$Select].Select() }
Pump 500
Save-Area $menu.Bounds 'menu.png'
$itemHeights = ($menu.Items | ForEach-Object { $_.Height }) -join ','

$style = $menu.Items | Where-Object { $_.Text -eq 'Move to' }
[void]$style.Select(); $style.ShowDropDown(); Pump 500
Save-Area ([Drawing.Rectangle]::Union($menu.Bounds, $style.DropDown.Bounds)) 'menu-submenu.png'

$menu.Close(); $menu.Dispose()

# Hide mode: the position submenu doesn't apply and is shown disabled.
$settings.Mode = [Enum]::Parse($assembly.GetType('CursorCloaker.Core.CloakMode'), 'Hide')
$hideMenu = [Activator]::CreateInstance($assembly.GetType('CursorCloaker.Ui.TrayMenu'), $flags, $null, @('v0.0.0-preview', $settings), $null).Menu
$hideMenu.Show((New-Object Drawing.Point 200, 200)); Pump 500
Save-Area $hideMenu.Bounds 'menu-hide-mode.png'
$hideMenu.Close(); $hideMenu.Dispose()
"Saved to $out  (menu $($menu.Bounds.Width)x$($menu.Bounds.Height), item heights: $itemHeights)"
