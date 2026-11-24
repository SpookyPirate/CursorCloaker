<#
  build.ps1 - test, build Release, and package CursorCloaker into dist\.

  Produces:
    dist\CursorCloaker\                 the portable app folder (exe + config)
    dist\CursorCloaker-v<version>.zip   the file to attach to a GitHub release

  The version comes from Directory.Build.props (the single source of truth), read through
  MSBuild so this script never parses or duplicates it.

  Usage (from the repo root):  powershell -ExecutionPolicy Bypass -File scripts\build.ps1
#>
$ErrorActionPreference = 'Stop'
$root    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\CursorCloaker\CursorCloaker.csproj'
$dist    = Join-Path $root 'dist'

function Invoke-Checked([string]$description, [scriptblock]$command) {
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$description failed (exit $LASTEXITCODE)" }
}

$version = (& dotnet msbuild $project -nologo -getProperty:Version).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+') { throw "Could not read Version from Directory.Build.props (got '$version')" }
Write-Host "CursorCloaker v$version"

Invoke-Checked 'Tests' { dotnet test (Join-Path $root 'CursorCloaker.slnx') --nologo -c Release }

$appDir = Join-Path $dist 'CursorCloaker'
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }
Invoke-Checked 'Build' { dotnet build $project --nologo -c Release -o $appDir }

# Ship only what the app needs at runtime; debug symbols stay out of the zip.
Get-ChildItem $appDir -File | Where-Object { $_.Name -notin 'CursorCloaker.exe', 'CursorCloaker.exe.config' } | Remove-Item -Force

$zip = Join-Path $dist "CursorCloaker-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $appDir '*') -DestinationPath $zip
Write-Host "Packaged $zip"
