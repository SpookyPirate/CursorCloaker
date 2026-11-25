# Handoff: CursorCloaker

This is everything a new developer or agent needs to continue the project cold. See also [README.md](README.md),
[RUNNING.md](RUNNING.md), [TECHSTACK.md](TECHSTACK.md) and the behaviour spec
[docs/DESIGN.md](docs/DESIGN.md).

## What exists and works (v0.1.0)

Every feature in DESIGN.md is implemented. On 2026-11-24 the user tested it with their real controllers and
reported it "works well". They didn't say which models. On 2026-11-25 they reported that streaming works perfectly.

- **Decision logic** (`Core/`):
  - `CloakPolicy`: a state machine for cloak and uncloak.
  - `InputFilters`: gamepad activity, mouse jitter, fullscreen geometry.
  - `CloakSettings` and `SettingsStore`: the .ini file, saved atomically next to the exe and written with defaults on first run.
- **Windows layer** (`Win32/`):
  - `SystemCursors`: blank-cursor hide, restore and park.
  - `XInputPoller`, plus `RawGamepadReader` for HID pads decoded via HidP_*.
  - `MouseWatcher`: a low-level hook on its own thread. It's armed only while cloaked or pending.
  - `ForegroundWatcher`, `StartupRegistration` (HKCU Run) and `ProgramCatalog`.
- **Engine** (`CloakEngine`): a 16 ms UI-thread timer polls XInput and fires delayed cloaks. The foreground is re-read every
  250 ms and before every controller decision.
- **App** (`TrayContext`):
  - The tray icon is dimmed while paused. Left or right click opens the menu.
  - Settings flow: menu or window → `ApplySettings` → save, engine, Run key, refresh.
  - On session lock, disconnect or sign-out, it uncloaks.
- **UI** (`Ui/`): dark with a blue accent, with every token in `Theme.cs`.
  - The themed menu, with submenus themed too.
  - `ThemedForm` (dark title bar), `ThemedButton` (primary/secondary, every state), `ProgramList`
    (icon rows with an empty state), `ListCard`, `SearchBox` (native cue banner).
  - `ExclusionsWindow` and `ProgramPickerWindow` (running programs or open windows, loaded off the UI thread, filtered as you type).
- **Icon**: `graphics/CursorCloaker-icon1.svg` covers 32-256 px. `graphics/CursorCloaker-icon1-small.svg` (simplified: heavier
  strokes, no motion trail) covers 16-24 px, through `scripts/make-icon.ps1`. The paused tray icon is the same frame with its
  contents greyed (`PAUSED_BRIGHTNESS` 0.6), with the tile kept solid. Fading the whole icon made it vanish on a dark taskbar.
- **Build**: `scripts/build.ps1` produces `CursorCloaker-v<version>.zip` → `CursorCloaker/` → files, and asserts that layout.
  The layout check was seen to flag a flat zip. CI is green. License: MIT.
- **Tests**: 67 passing (`dotnet test`). The mouse-uncloak rule was deliberately broken to confirm the suite
  catches it.

**Verified facts (2026-11-24, Windows 11, developer machine):**
- **Cursor hide/restore:** the arrow, I-beam and hand cursors were drawn and their opaque pixels counted:
  144/26/282 before, 0 after `Hide`, and 144/26/282 after `Restore`.
- **Hook probe:** `SetCursorPos` never reaches a `WH_MOUSE_LL` hook. `SendInput` does, flagged injected
  (see DESIGN.md "Mouse input").
- **Engine in a harness:**
  - The mouse hook installs and Raw Input registers.
  - The engine starts uncloaked.
  - The foreground read works (Brave, windowed: not fullscreen) and the engine disposes cleanly.
- **The exe:**
  - It runs. `StartWithWindows=false` leaves no Run entry.
  - `true` writes `"<exe path>"`, and switching back to `false` removes it. The registry was left as found.
- **Screenshots, looked at:**
  - The menu, and the menu with a submenu open.
  - The exclusions window, empty and filled, with the Add menu open.
  - Both pickers, with real data: 92 running programs and 13 windows.

## Since v0.1.0 (unreleased; version already bumped to 0.2.0)

- **Park position setting** (`ParkPosition`, in the tray's **Cloak style** submenu under a "Move to" heading): any corner or the middle of any edge, default bottom-right.
  Tested: every position has a unit test (a deliberately swapped position was caught), and the real cursor was parked
  at `TopLeft` → 0,0 and `RightEdge` → 2559,719 on a 2560×1440 screen. Menu screenshots reviewed.

## Releases

- **v0.1.0** (2026-11-24): https://github.com/SpookyPirate/CursorCloaker/releases/tag/v0.1.0. The `CursorCloaker-v0.1.0.zip` asset is
  184,845 bytes, verified equal to the local build. The tag is on commit 165e40a. The zip is mostly the icon (embedded twice: once as
  the exe icon, once as the tray resource).

## Gotchas

- **SetSystemCursor survives the process.** Killing the app while it's cloaked (Task Manager, `Stop-Process`)
  leaves the cursor invisible until the next launch, sign-out or Mouse settings change. The startup
  restore exists for this reason. Don't remove it.
- **Dev runs register themselves to start with Windows** unless the dev folder's ini sets
  `StartWithWindows=false` (see RUNNING.md).
- The mouse hook **must** stay on its own thread. Windows silently drops a low-level hook that's slow to
  answer.
- **Preview harness:** `PrintWindow(PW_RENDERFULLCONTENT)` leaves owner-drawn regions stale on screen, so the
  harness captures from the screen instead. A "blank row" seen in an early capture was this artifact, not an app bug.
- PowerShell harness scripts must pass `.psobject.BaseObject` (or typed arrays) to reflection `Invoke`.
- The solution is `CursorCloaker.slnx`, which needs SDK 9.0.200 or later. `TreatWarningsAsErrors` is on.

## What the tests do not cover

The unit tests cover `Core/` only. Nothing in `Win32/`, `Ui/` or `CloakEngine` is unit-tested. They were
checked by the probes and screenshots above.

## Never run / not yet verified

- **Per-model controller coverage.** The user's real-controller test passed (models not reported). Raw Input pads
  (Switch Pro, DualShock/DualSense) haven't been individually confirmed, and neither have the dead-zone and drift thresholds on a drifting stick.
- **Parking corner edge case:** while the cursor is parked in a corner, moving the mouse *into* that corner may not register (the hook position may be clamped there); clicks still do. Not yet reported either way.
- A Switch Pro Controller that Steam has switched into its full (0x30) report mode. This is expected *not* to be
  detected (see RawGamepadReader's header).
- Multiple monitors, Windows 10, and 100% or 200% scaling.
- The tray icon as it appears in the real notification area. Windows 11 put it in the hidden overflow, so it was judged by
  rendering it on the taskbar's own colours (#202020 dark, #F3F3F3 light) at the real 16 px tray size, enabled and paused.

## Next steps

1. Gather feedback from real use, especially streaming and Switch Pro under Steam, and tune the `GamepadActivity` thresholds and
   `MouseMovementFilter.MOVE_THRESHOLD_PIXELS` from it.
2. The README images come from `scripts/readme-screenshots.ps1`. Re-shoot them after UI changes.
