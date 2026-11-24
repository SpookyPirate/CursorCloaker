# CursorCloaker design

The agreed behaviour and the planned components. [TECHSTACK.md](../TECHSTACK.md) covers how it is
built, and [HANDOFF.md](../HANDOFF.md) covers what exists today.

## The problem

When you play with a controller on a PC, especially from the couch or over Moonlight or Steam Link, the
mouse cursor sits on top of the game or Big Picture. Existing tools such as AutoHideMouseCursor
hide it after a period of no mouse movement **everywhere in Windows**. They can't tell "I'm playing
with a pad" apart from "I stopped moving the mouse while reading". CursorCloaker hides the cursor
only because a controller is in use.

## Behaviour

**Cloak** the cursor when all of these are true:

1. A controller button is pressed or a stick or trigger moves past its dead zone, and
2. the foreground window is in scope:
   - `Scope=FullscreenOnly` (the default): the window covers its whole monitor, either fullscreen
     or borderless. That covers Big Picture, RetroBat, emulators and fullscreen games.
   - `Scope=AllPrograms`: any foreground window.
3. the foreground process is not in `Exclude`, and
4. CursorCloaker is enabled.

**Uncloak** immediately when:

- the real mouse moves past a small jitter threshold, or is clicked,
- the foreground window leaves the scope (switching to a windowed app or the desktop),
- the user disables CursorCloaker or exits it.

Using only the mouse never cloaks anything.

**The mouse always wins until the controller is used again.** Once the mouse uncloaks the
cursor, it stays visible however long the mouse sits still. Only the next controller input
cloaks it again. So switching back and forth between pad and mouse in the same game just works:
pad hides the cursor, mouse shows it, pad hides it.

## Modes

| Mode | What it does | Why you'd want it |
| --- | --- | --- |
| `Hide` | Swaps every system cursor for a blank one | Works in every program that uses the system cursor |
| `Park` | Moves the cursor to the bottom-right corner of its monitor | For programs that draw their own cursor |
| `HideAndPark` (default) | Both | Hidden, and out of the way if a program ignores the swap |

When it uncloaks, `Park` does not move the cursor back. The corner is a harmless place for it to
reappear.

## Safety invariant: the cursor must never stay invisible

Hide uses `SetSystemCursor`. Windows keeps that change after the process exits, until something
reloads the cursor scheme. So `SystemCursors.Restore()` (SPI_SETCURSORS) runs:

- at every startup, in case a previous run was killed,
- on normal exit, on an unhandled exception and on ProcessExit,
- when the session is locked, disconnected or signed out (`SessionSwitch`).

If the user ever sees no cursor, signing out and back in, or reopening Mouse settings, also
restores it.

## Planned components

```
Program            single instance, restore cursors on every exit path
TrayContext        tray icon + menu, owns the components below
Core/              pure logic, unit tested
  CloakSettings    .ini format, defaults, validation            (done)
  SettingsStore    load/save next to the exe                    (done)
  CloakPolicy      decides cloak/uncloak from input, foreground and settings   (done)
  InputFilters     gamepad activity, mouse jitter, fullscreen geometry          (done)
Win32/             thin P/Invoke wrappers, no decisions
  SystemCursors    blank-cursor hide, restore, park                (done)
  XInputPoller     XInput slots 0-3, polled at ~60 Hz              (done)
  RawGamepadReader Raw Input HID gamepads, decoded via HidP_*      (done)
  MouseWatcher     low-level mouse hook on its own thread          (done)
  ForegroundWatcher foreground process + fullscreen test           (done)
  StartupRegistration "Start with Windows" via HKCU Run key     (done)
  ProgramCatalog   programs from an exe, running processes, open windows (done)
CloakEngine        wires watchers -> policy -> SystemCursors       (done)
Ui/                dark theme and windows
  Theme            colour, type and spacing tokens              (done)
  ThemedMenu       dark renderer + Win11 rounded frame for menus (done)
  TrayMenu         the tray menu                                (done)
  ThemedForm/Button, ProgramList, ListCard, SearchBox   themed window parts (done)
  ExclusionsWindow + ProgramPickerWindow                add/remove excluded programs (done)
```

`CloakPolicy` takes plain values (whether a controller was active, whether there was mouse
activity, the foreground window's bounds and its monitor's bounds, the process name, the
settings) and returns a decision. That keeps every rule above testable without a real
controller.

### Controller input

- **XInput** (`XInputGetState` across slots 0-3, polled at about 60 Hz): Xbox pads and 8BitDo pads in
  XInput mode.
- **Raw Input** (`RegisterRawInputDevices`, usage page 0x01, usages 0x04 joystick and 0x05
  gamepad, `RIDEV_INPUTSINK` so input still arrives in the background): Switch Pro, DualShock and
  DualSense, and pads in DInput mode.
- There's no dependency on Steam Input. When Steam Input is translating a pad, its virtual XInput
  device is picked up the same way.
- Raw Input reports arrive continuously from some pads even when idle. Activity means a change in
  buttons, or axes moving past a dead zone, compared with that device's previous report.

### Mouse input

A low-level mouse hook (`WH_MOUSE_LL`) sees mouse movement, clicks and wheel. **Injected events
count as real.** Measured on Windows 11 (2026-11-24):
- `SetCursorPos` never reaches the hook. So the app's own parking, and games recentering the
  cursor, can't uncloak it.
- `SendInput` does reach the hook, with `LLMHF_INJECTED`. That's how Moonlight/Sunshine and Steam Link
  deliver the couch mouse. Ignoring injected input would leave a streaming user unable to bring the
  cursor back.

Movement goes through a jitter filter (6 px since cloaking). Clicks and wheel count at once. The
hook runs on its own thread with its own message loop. Windows silently removes a low-level hook
whose thread is slow to respond (`LowLevelHooksTimeout`), so UI work must never be able to block it.

### Fullscreen test

The foreground window's rectangle covers its monitor's full bounds (`MonitorFromWindow` +
`GetMonitorInfo`), with a few pixels of tolerance because Big Picture is sometimes a pixel short. The
desktop (`Progman` / `WorkerW`) and the taskbar never count. The process is DPI aware
(app.manifest), so the rectangles are physical pixels.

## Tray menu

Left or right click opens it. While paused, the tray icon is dimmed and its tooltip says so.

- **Enabled**: on or off.
- **Cloak style**: Hide, Move to corner, or Hide and move to corner (`Mode`).
- **Cloak over**: Fullscreen programs or All programs (`Scope`).
- **Excluded programs…**: opens the exclusions window.
- **Start with Windows**: on or off.
- **Exit**: restores the cursors and closes.

## Exclusions window

A small window for managing the programs CursorCloaker never cloaks over. Users shouldn't have to
edit the .ini file.

- **List**: each excluded program shows its icon, its name (from the exe's file description, falling
  back to the file name) and its process name. Empty state: one line saying nothing is
  excluded, plus the Add action.
- **Remove**: removes the selected program (also with the Delete key). Removing is instant and easy to
  redo, so there's no confirmation.
- **Add** offers three sources:
  1. **Browse…**: a file dialog filtered to `.exe`.
  2. **Running programs**: a searchable list of running processes that have a path the user can
     access, one row per exe, with icon and name.
  3. **Open windows**: the visible top-level windows, by title and owning program. This is the quickest
     way to pick the game that's open right now.
  All three resolve to the same thing, a process name stored in `Exclude=`. A program that's already
  excluded is shown as such, not added twice.
- Changes save immediately to `CursorCloaker.ini` and apply without a restart.

## UI standard

All UI is **dark with a blue accent** and follows the global `ui-design` skill. Every colour, font
and spacing value comes from `src/CursorCloaker/Ui/Theme.cs`. No other file defines a colour.
WinForms controls are themed through shared helpers (`ThemedMenu` for menus; a themed window
base `ThemedForm`, `ThemedButton`, `ProgramList`, `ListCard` and `SearchBox` for windows). Default light
system styling must never show. Each UI change is checked by rendering it and looking at the
screenshot: `scripts/preview-menu.ps1` for the menu, `scripts/preview-windows.ps1` for the windows.

## Streaming

Moonlight and Steam Link show the host's real cursor, so cloaking on the host works the same while
you stream. Nothing special is needed.
