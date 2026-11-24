# CLAUDE.md

## Project overview
CursorCloaker is a Windows tray app that hides the mouse cursor while a controller is in use. It works over
fullscreen windows by default, or over all programs. The mouse always brings the cursor back.
The spec is in @docs/DESIGN.md.

## Tech stack
- C# targeting .NET Framework 4.8 (in-box on Win10/11), WinForms, built with the .NET SDK 10 (`global.json`)
- Win32 via P/Invoke: SetSystemCursor/SPI_SETCURSORS, XInput, Raw Input, low-level mouse hook, DWM
- xUnit for `Core/`

## Structure
| Path | Purpose |
| --- | --- |
| src/CursorCloaker/Core | pure logic (settings, cloak policy), unit tested |
| src/CursorCloaker/Win32 | thin P/Invoke wrappers, no decisions |
| src/CursorCloaker/Ui | Theme tokens + themed menus/windows |
| tests/CursorCloaker.Tests | xUnit |
| scripts/ | build.ps1 (test+package), preview-menu.ps1 (UI screenshot) |

## Commands
```powershell
dotnet build
dotnet test
powershell -ExecutionPolicy Bypass -File scripts\build.ps1         # -> dist\CursorCloaker-v<ver>.zip
powershell -ExecutionPolicy Bypass -File scripts\preview-menu.ps1  # -> preview\menu.png
```

## Conventions
- Follow the global `coding-standards` and `working-standards` skills. Commit and push after each major change.
- **UI:** dark with a blue accent. Follow the global `ui-design` skill (`~/.claude/skills/ui-design`) for all UI
  work. Every colour, font and spacing value comes from `Ui/Theme.cs`. Render every UI change and look at it
  before calling it done.
- Version lives only in `Directory.Build.props`.
- Keep the docs current: README, RUNNING, TECHSTACK, HANDOFF, docs/DESIGN.md.

## Gotchas
- Any code that swaps the system cursor must be paired with `SystemCursors.Restore()` on every exit path.
  A killed process otherwise leaves the user with no visible cursor.
- Check that nobody is gaming or streaming on the PC before running UI previews or live cursor tests.
  They act on the real screen and cursor.
