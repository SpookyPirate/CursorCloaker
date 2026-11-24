# Running and building CursorCloaker

This is the command reference for day-to-day development. For architecture, see [TECHSTACK.md](TECHSTACK.md).
For what exists today, see [HANDOFF.md](HANDOFF.md).

> Run every command below **from the repo root**, in PowerShell.

## Layout

```
src/CursorCloaker/        the tray app (WinExe, .NET Framework 4.8, WinForms)
  Core/                   pure logic: settings, and later the cloak policy (unit tested)
  Win32/                  thin P/Invoke wrappers (cursors; later input and foreground)
  Ui/                     dark theme tokens (Theme.cs) and themed menus/windows
tests/CursorCloaker.Tests xUnit tests for Core
scripts/build.ps1         test + Release build + zip into dist/
scripts/preview-menu.ps1  screenshot the real tray menu (and a submenu) into preview/
scripts/preview-windows.ps1 screenshot the exclusions window and pickers into preview/
scripts/make-icon.ps1     rebuild Resources\CursorCloaker.ico from graphics\CursorCloaker-icon1.svg
graphics/                 icon artwork (the SVG is the master)
docs/DESIGN.md            agreed behaviour and planned components
Directory.Build.props     version (single source of truth) + shared build settings
global.json               pins the .NET SDK (10.0.x)
```

## First-time setup

Install the .NET SDK 10. That's all; NuGet packages restore on the first build.

```powershell
dotnet --list-sdks
```

## Everyday dev loop

```powershell
dotnet build
.\src\CursorCloaker\bin\Debug\net48\CursorCloaker.exe
```

The app sits in the tray. Click its icon and choose **Exit**.

> **Before the first dev run:** put a `CursorCloaker.ini` containing `StartWithWindows=false` in
> `src\CursorCloaker\bin\Debug\net48\`. Otherwise the default (on) registers the *dev* exe to start
> with Windows. Only one copy runs per session, so
a second launch exits immediately. Stop the running copy before testing a new build. If it's stuck:

```powershell
Stop-Process -Name CursorCloaker
```

> Stopping it with `Stop-Process` skips the normal exit path. The next launch restores the cursors at
> startup. That restore is deliberate; see the safety invariant in [docs/DESIGN.md](docs/DESIGN.md).

## Looking at the UI

Every UI change gets rendered and looked at (see the UI standard in [docs/DESIGN.md](docs/DESIGN.md)). For the tray menu:

```powershell
dotnet build; powershell -ExecutionPolicy Bypass -File scripts\preview-menu.ps1
dotnet build; powershell -ExecutionPolicy Bypass -File scripts\preview-windows.ps1
```

These open the real menu and windows on the primary screen for a few seconds, using data from this PC, and
save PNGs to `preview\` (gitignored). Don't run them while someone is gaming or streaming.

## App icon

`graphics\CursorCloaker-icon1.svg` is the artwork for 32 px and up. `graphics\CursorCloaker-icon1-small.svg` is a simplified
version for the 16-24 px tray sizes, where the full artwork's fine lines blur. After editing either one, regenerate the
committed .ico (16-256 px frames). This needs ImageMagick 7 on PATH; normal builds don't.

```powershell
powershell -ExecutionPolicy Bypass -File scripts\make-icon.ps1
```

## Tests

```powershell
dotnet test
```

## Versioning and release

Bump **`<Version>` in `Directory.Build.props`** and nowhere else. The exe's file and product version, the
version shown in the tray, and the zip name all come from it. Then:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

That runs the tests and produces `dist\CursorCloaker\` and `dist\CursorCloaker-v<version>.zip`. The zip holds one
`CursorCloaker/` folder (no version in its name), so a release extracts straight over the previous one. The script
fails if any entry lands outside that folder, or if a `CursorCloaker.ini` would ship and overwrite user settings.
Smoke-test the packaged exe before publishing:

```powershell
Stop-Process -Name CursorCloaker -ErrorAction SilentlyContinue
$p = Start-Process .\dist\CursorCloaker\CursorCloaker.exe -PassThru; Start-Sleep 2; "alive: $(-not $p.HasExited)"; (Get-Item .\dist\CursorCloaker\CursorCloaker.exe).VersionInfo.ProductVersion; Stop-Process -Id $p.Id
```

Then publish:

```powershell
git tag -a v0.1.0 -m "CursorCloaker v0.1.0"; git push origin v0.1.0
gh release create v0.1.0 dist\CursorCloaker-v0.1.0.zip --title "CursorCloaker v0.1.0" --notes "..."
```

## Data and settings

`CursorCloaker.ini` sits **next to the exe** (portable app), and it's gitignored. If it's missing or
can't be read, the defaults are used. There's no other data and there are no secrets.

## Ports and environment variables

None. It's a local tray app with no network access.
