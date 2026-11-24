# CursorCloaker

CursorCloaker is a small Windows tray app that hides the mouse cursor while you're playing with a controller, and only then.
Press a button or move a stick in a fullscreen game, in Steam Big Picture or in an emulator, and the
cursor disappears. Touch the mouse and it's back. Ordinary desktop use is never affected.

> **Status: v0.1.0, feature-complete and awaiting real-controller testing.** See [Status](#status).

## Download

Builds will be published on the [Releases page](https://github.com/SpookyPirate/CursorCloaker/releases/latest)
as a portable zip. It contains a single `CursorCloaker` folder. Extract it somewhere permanent and run
`CursorCloaker\CursorCloaker.exe`; there's nothing to install. To upgrade, extract the new zip to the same place: the new
folder lands on the old one, and your settings are kept. It needs Windows 10 or 11, which already include
.NET Framework 4.8.

> Windows 11 hides new tray icons in the overflow (the **^** next to the clock). To keep CursorCloaker
> visible, drag its icon onto the taskbar, or turn it on under **Settings > Personalization > Taskbar >
> Other system tray icons**.

> Builds are unsigned, so Windows SmartScreen may warn the first time you run one. Choose **More info**, then **Run anyway**.

## Features

- **Controller-triggered.** Reacts to XInput pads (Xbox, 8BitDo) and to other HID gamepads (Switch
  Pro, DualShock/DualSense) through Raw Input. It doesn't need Steam Input.
- **Scope setting.** Cloak only over fullscreen or borderless windows (the default), or over any program.
- **Modes.** Hide the cursor, park it in the corner, or both (the default).
- **Never stuck invisible.** The normal cursors are restored on start, on exit, after a crash and when the PC locks.
- **Mouse always wins.** Touch the mouse and the cursor stays visible until you use the controller again.
- **Excluded programs.** Pick programs it should never touch: browse to an exe, or choose from running programs or open windows.
- **Tray menu.** Enable or disable, mode, scope, excluded programs, start with Windows, exit.
- **Dark UI** with blue accents throughout.
- **Works while streaming** with Moonlight or Steam Link.

The full behaviour is described in [docs/DESIGN.md](docs/DESIGN.md).

## Settings

`CursorCloaker.ini` is created next to the exe:

```ini
[CursorCloaker]
Enabled=true
Mode=HideAndPark          ; Hide | Park | HideAndPark
Scope=FullscreenOnly      ; FullscreenOnly | AllPrograms
HideDelayMs=0
StartWithWindows=true
Exclude=                  ; e.g. obs64;notepad
```

## Build from source

You need the .NET SDK 10 (see `global.json`). From the repo root:

```powershell
dotnet test
powershell -ExecutionPolicy Bypass -File scripts\build.ps1   # -> dist\CursorCloaker-v<version>.zip
```

[RUNNING.md](RUNNING.md) has every command. [TECHSTACK.md](TECHSTACK.md) explains how it's built.
[HANDOFF.md](HANDOFF.md) describes the current state.

## Status

- [x] Tray app scaffold: single instance, restores the cursors on every exit path
- [x] Settings file: parsing, defaults, validation, tests
- [x] Build script and CI
- [x] Blank-cursor hide, and parking
- [x] Controller input (XInput + Raw Input)
- [x] Mouse watcher and foreground/fullscreen detection
- [x] Cloak policy and its tests
- [x] Dark theme for the tray menu
- [x] Tray menu settings, Start with Windows
- [x] Excluded programs window (browse / running programs / open windows)
- [ ] Tested with real controllers (Xbox/8BitDo, Switch Pro, DualSense) and streaming

## License

[MIT](LICENSE)
- [x] App icon (exe + tray)
