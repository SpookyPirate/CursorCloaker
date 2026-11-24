<#
  preview-menu.ps1 - render the real tray menu and save a screenshot, for visual review.

  Loads the built CursorCloaker.exe, builds its menu through TrayMenu.Create, adds sample items
  so every state is visible (checked, unchecked, disabled, submenu), selects one item to show the
  hover state, opens the menu on screen, captures it, and closes it.

  Usage (from the repo root, after dotnet build):
    powershell -ExecutionPolicy Bypass -File scripts\preview-menu.ps1 [-Out preview\menu.png] [-Select 3]

  Note: it opens a real menu on the primary screen for about a second.
#>
param(
    [string]$Out = 'preview\menu.png',
    [int]$Select = 3,
    [switch]$Release
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configuration = if ($Release) { 'Release' } else { 'Debug' }
$exe = Join-Path $root "src\CursorCloaker\bin\$configuration\net48\CursorCloaker.exe"
if (-not (Test-Path $exe)) { throw "Build first: $exe not found" }

Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public static class PreviewDpi { [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value); }
'@
[void][PreviewDpi]::SetProcessDpiAwarenessContext([IntPtr]::new(-4))   # per-monitor v2, as the app

$assembly = [Reflection.Assembly]::LoadFrom($exe)
$trayMenu = $assembly.GetType('CursorCloaker.Ui.TrayMenu')
$menu = $trayMenu.GetMethod('Create', [Reflection.BindingFlags]'Static,Public').Invoke($null, @('v0.0.0-preview', [Action]{ }))

# Sample items covering every state the real menu will use.
$enabled = New-Object Windows.Forms.ToolStripMenuItem 'Enabled'; $enabled.Checked = $true
$mode = New-Object Windows.Forms.ToolStripMenuItem 'Mode'
foreach ($name in 'Hide', 'Park', 'Hide and park') { $sub = $mode.DropDownItems.Add($name); if ($name -eq 'Hide and park') { $sub.Checked = $true } }
$startup = New-Object Windows.Forms.ToolStripMenuItem 'Start with Windows'
$disabled = New-Object Windows.Forms.ToolStripMenuItem 'Open settings file'; $disabled.Enabled = $false
$menu.Items.Insert(2, $enabled); $menu.Items.Insert(3, $mode); $menu.Items.Insert(4, $startup); $menu.Items.Insert(5, $disabled)
$menu.Items.Insert(6, (New-Object Windows.Forms.ToolStripSeparator))

$origin = New-Object Drawing.Point 200, 200
$menu.Show($origin)
if ($Select -ge 0 -and $Select -lt $menu.Items.Count) { [void]$menu.Items[$Select].Select() }
for ($i = 0; $i -lt 20; $i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 25 }

# Capture the menu plus a margin so the corners and shadow edge are visible.
$margin = 24
$bounds = $menu.Bounds
$area = New-Object Drawing.Rectangle ($bounds.X - $margin), ($bounds.Y - $margin), ($bounds.Width + 2 * $margin), ($bounds.Height + 2 * $margin)
$bitmap = New-Object Drawing.Bitmap $area.Width, $area.Height
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($area.Location, [Drawing.Point]::Empty, $area.Size)
$itemHeights = ($menu.Items | ForEach-Object { $_.Height }) -join ','
$menu.Close(); $menu.Dispose()

$outPath = Join-Path $root $Out
New-Item -ItemType Directory -Force (Split-Path $outPath) | Out-Null
$bitmap.Save($outPath, [Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $bitmap.Dispose()
"Saved $outPath  (menu $($bounds.Width)x$($bounds.Height), item heights: $itemHeights)"
