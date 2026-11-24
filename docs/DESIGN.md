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
- when the session is locked or the user signs out (planned, `SessionSwitch`).

If the user ever sees no cursor, signing out and back in, or reopening Mouse settings, also
restores it.

## Planned components

```
Program            single instance, restore cursors on every exit path
TrayContext        tray icon + menu, owns the components below
Core/              pure logic, unit tested
  CloakSettings    .ini format, defaults, validation            (done)
  SettingsStore    load/save next to the exe                    (done)
  CloakPolicy      decides cloak/uncloak from input, foreground and settings   (planned)
Win32/             thin P/Invoke wrappers, no decisions
  SystemCursors    restore (done), blank-cursor swap (planned)
  ControllerInput  XInput polling + Raw Input HID gamepads (planned)
  MouseWatcher     low-level mouse hook that ignores injected events (planned)
  ForegroundWatch  foreground window, its process, fullscreen test (planned)
  Startup          "Start with Windows" via HKCU Run key (planned)
Ui/                dark theme and windows
  Theme            colour, type and spacing tokens              (done)
  ThemedMenu       dark renderer + Win11 rounded frame for menus (done)
  TrayMenu         builds the tray menu                         (done: version + Exit)
  ExclusionsWindow add/remove excluded programs                 (planned)
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

A low-level mouse hook (`WH_MOUSE_LL`) sees real movement. Events flagged `LLMHF_INJECTED` are
ignored, so the app's own parking and any software-generated movement don't uncloak the cursor.
Open question: Steam Input's mouse emulation is injected, so it won't uncloak. Decide whether
it should.

### Fullscreen test

The foreground window's rectangle covers its monitor's full bounds (`MonitorFromWindow` +
`GetMonitorInfo`), with a few pixels of tolerance because Big Picture is sometimes a pixel short. The
desktop (`Progman` / `WorkerW`) and the taskbar never count. The process is DPI aware
(app.manifest), so the rectangles are physical pixels.

## Tray menu (planned)

- **Enabled**: on or off.
- **Mode**: Hide, Park, or Hide + Park.
- **Scope**: Fullscreen only, or All programs.
- **Excluded programs…**: opens the exclusions window.
- **Start with Windows**: on or off.
- **Exit**: restores the cursors and closes.

## Exclusions window (planned)

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
base and its controls when the exclusions window is built). Default light system styling must
never show. Each UI change is checked by rendering it (`scripts/preview-menu.ps1` for the menu)
and looking at the screenshot.

## Streaming

Moonlight and Steam Link show the host's real cursor, so cloaking on the host works the same while
you stream. Nothing special is needed.
