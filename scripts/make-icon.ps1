<#
  make-icon.ps1 - build the app icon (.ico) from the master SVG.

  graphics\CursorCloaker-icon1.svg is the artwork for 32 px and up. graphics\CursorCloaker-icon1-small.svg
  is a simplified version of it for 16-24 px (the tray at 100-150% scaling), where the full
  artwork's fine lines and motion trail blur into a smudge. Each is rendered once at high
  resolution with ImageMagick and downsampled to every size Windows asks for: 16-48 for the tray,
  title bars and small views (each DPI step from 100% to 200%), 64-256 for Explorer and the taskbar.
  The result is committed, so normal builds don't need ImageMagick.

  Usage (from the repo root):  powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
  Requires: ImageMagick 7 (magick) on PATH.
#>
$ErrorActionPreference = 'Stop'
$root   = Split-Path -Parent $PSScriptRoot
$svg      = Join-Path $root 'graphics\CursorCloaker-icon1.svg'
$smallSvg = Join-Path $root 'graphics\CursorCloaker-icon1-small.svg'
# Sizes at or below this use the small-size artwork.
$SMALL_ARTWORK_MAX = 24
$ico    = Join-Path $root 'src\CursorCloaker\Resources\CursorCloaker.ico'
$sizes  = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$work   = Join-Path ([IO.Path]::GetTempPath()) ('cursorcloaker-icon-' + [Guid]::NewGuid().ToString('N'))

if (-not (Get-Command magick -ErrorAction SilentlyContinue)) { throw 'ImageMagick (magick) is required' }
New-Item -ItemType Directory -Force $work, (Split-Path $ico) | Out-Null
try {
    # 1024-unit SVG at density 384 -> 4096 px master, so every size is a clean downsample.
    $master = Join-Path $work 'master.png'
    $smallMaster = Join-Path $work 'master-small.png'
    foreach ($pair in @(@($svg, $master), @($smallSvg, $smallMaster))) {
        & magick -background none -density 384 $pair[0] $pair[1]
        if ($LASTEXITCODE -ne 0) { throw "SVG render failed: $($pair[0])" }
    }

    $frames = foreach ($size in $sizes) {
        $frame = Join-Path $work "$size.png"
        # Lanczos keeps edges crisp when shrinking; small sizes get a light sharpen to stay legible.
        $sharpen = if ($size -le 48) { @('-unsharp', '0x0.6+0.8+0') } else { @() }
        $source = if ($size -le $SMALL_ARTWORK_MAX) { $smallMaster } else { $master }
        & magick $source -filter Lanczos -resize "${size}x${size}" @sharpen $frame
        if ($LASTEXITCODE -ne 0) { throw "Resize to $size failed" }
        $frame
    }
    & magick @frames $ico
    if ($LASTEXITCODE -ne 0) { throw 'ICO assembly failed' }
    Write-Host "Wrote $ico ($($sizes -join ', ') px)"
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
