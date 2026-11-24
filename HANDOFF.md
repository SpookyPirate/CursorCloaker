# Handoff: CursorCloaker

This is everything a new developer or agent needs to continue the project cold. See also [README.md](README.md),
[RUNNING.md](RUNNING.md), [TECHSTACK.md](TECHSTACK.md) and the behaviour spec
[docs/DESIGN.md](docs/DESIGN.md).

## What exists and works (v0.1.0 scaffold)

- **Tray app** (`src/CursorCloaker`): a .NET Framework 4.8 WinForms `WinExe`. Only one instance runs per
  session (`Local\CursorCloaker.SingleInstance` mutex). It restores the system cursors at startup,
  on exit, on an unhandled exception and on ProcessExit. The tray icon menu shows the version and Exit.
- **Settings** (`Core/CloakSettings`, `Core/SettingsStore`): .ini format, defaults, validation,
  atomic save. Loaded at startup, not yet used.
- **Dark UI** (`Ui/Theme`, `Ui/ThemedMenu`, `Ui/TrayMenu`): custom menu renderer and the Win11 rounded
  DWM frame. All tokens live in `Theme.cs`.
- **Build** (`scripts/build.ps1`): tests, then Release, then `dist\CursorCloaker-v0.1.0.zip` (exe + config).
- **CI** (`.github/workflows/ci.yml`): runs the build script on windows-latest.
- **Tests**: 14 passing (`dotnet test`). They were seen to fail when a default was deliberately broken.

**Verified facts:**
- The exe is about 14 KB.
- ProductVersion reads `0.1.0`.
- A second launch exits with code 0 while the first keeps running.
- The packaged `dist` exe launches and stays running.
- The menu renders dark with a blue checkmark. Screenshot via `scripts/preview-menu.ps1` at the
  developer's DPI: items are 38 px tall, the menu is 291×244 px with sample items.
- No ports, no network access, no secrets.

## Gotchas

- **SetSystemCursor survives the process.** Once hiding is implemented, killing the app (Task Manager,
  `Stop-Process`) leaves the blank cursor in place until the next launch, sign-out or Mouse settings
  change. The startup restore exists for this reason. Don't remove it.
- `Stop-Process` skips managed exit handlers, so only the startup restore covers it.
- The solution file is `CursorCloaker.slnx` (new XML format). It needs .NET SDK 9.0.200 or later; `global.json` pins 10.0.x.
- `TreatWarningsAsErrors` is on for every project.
- `preview-menu.ps1` opens a real menu on the primary screen. Don't run it while someone is
  gaming or streaming.

## What the tests do not cover

Everything in `Win32/` and `Ui/`, and the whole cloaking feature, which doesn't exist yet. No test
uses a real controller, mouse hook, fullscreen window or DPI other than the developer's.

## Never run / not yet verified

- The CI workflow before its first push (check the Actions tab).
- The Windows 10 code path (`DrawsOwnBorder`, where DWM rounding is unavailable).
- Submenus and a pressed state in the themed menu. The preview shows hover, checked and disabled, but not an
  open submenu.
- Behaviour at 100% or 200% display scaling.

## Next steps, in order

1. `Win32/SystemCursors`: blank-cursor hide (one `CreateCursor` blank cursor, copied into each
   OCR_* system cursor id), plus parking.
2. `Core/CloakPolicy` with tests: every rule in DESIGN.md, including "the mouse wins until the next
   controller input".
3. `Win32/ControllerInput` (XInput + Raw Input), `MouseWatcher` (LL hook, ignore injected),
   `ForegroundWatch` (fullscreen test). Then wire them into `TrayContext`.
4. Session lock/sign-out restore (`SystemEvents.SessionSwitch`).
5. Full tray menu, Start with Windows (HKCU Run), and the exclusions window, all dark-themed
   through `Ui/`.
6. App icon, then the v0.1.0 → v1.0.0 release via RUNNING.md.
