# CursorCloaker tech stack

How CursorCloaker is built, and why. Behaviour is covered in [docs/DESIGN.md](docs/DESIGN.md),
commands in [RUNNING.md](RUNNING.md), and current state in [HANDOFF.md](HANDOFF.md).

## Shape of the app

```
            ┌──────────────── CursorCloaker.exe (tray, no main window) ────────────────┐
 controller │ Win32/ControllerInput ─┐                                                 │
 mouse      │ Win32/MouseWatcher ────┼──► Core/CloakPolicy ──► Win32/SystemCursors     │
 foreground │ Win32/ForegroundWatch ─┘        ▲   (pure)        (hide / park / restore)│
            │                          Core/CloakSettings ◄── Core/SettingsStore (.ini) │
            │ Ui/ (Theme, ThemedMenu, TrayMenu, ExclusionsWindow) ◄── TrayContext       │
            └──────────────────────────────────────────────────────────────────────────┘
```

**The idea behind every choice: a tiny, dependency-free background utility that can never leave
the user without a cursor.** It should be small enough to forget it's running, and safe enough that a crash
costs nothing.

## Dependencies

| Dependency | Version | What it does here |
| --- | --- | --- |
| .NET Framework | 4.8 (target) | Runtime. Ships with Windows 10/11, so users install nothing and the exe is about 15 KB |
| WinForms | in-box | The tray icon (`NotifyIcon`), its context menu and the exclusions window |
| .NET SDK | 10.0.x (`global.json`) | Builds the net48 project with `dotnet build`, so Visual Studio isn't required |
| Microsoft.NETFramework.ReferenceAssemblies | 1.0.3 | net48 reference assemblies for the SDK build (build-time only) |
| xUnit + Microsoft.NET.Test.Sdk | 2.9.2 / 17.11.1 | Unit tests for `Core/` |
| ImageMagick | 7.x (dev only) | `scripts/make-icon.ps1` renders the master SVG to the multi-size .ico. The .ico is committed, so builds and CI don't need it |
| Win32 APIs (P/Invoke) | — | `SetSystemCursor` / `SPI_SETCURSORS` (hide and restore), XInput + Raw Input (controllers), `WH_MOUSE_LL` (mouse), `GetForegroundWindow` + `MonitorFromWindow` (fullscreen), DWM attributes (Win11 menu frame) |

### Why .NET Framework 4.8 and not .NET 8/10

- **Chosen:** net48 is preinstalled on every supported Windows, so the release is a single small exe
  with nothing to install.
- **Trade-off:** older C# runtime features (no `init`, records need polyfills), and WinForms
  without .NET's newer high-DPI and dark-mode APIs. That's why theming is hand-rolled in `Ui/`.
- **Time to change:** if a feature needs an API only modern .NET has, or Windows stops
  shipping 4.8. The fallback would be .NET 10 self-contained single-file. It needs no install but is
  roughly 60+ MB.

### Why WinForms and not WPF / WinUI

The app's UI is a tray menu and one small window. WinForms is the lightest in-box option and
has a first-class `NotifyIcon`. The cost is manual dark theming, which `Ui/` already handles.

## Layers

- **Core/**: pure logic, no Win32 or file I/O except in `SettingsStore`. Fully unit-tested.
  `CloakPolicy` (planned) takes plain values and returns a decision, so every rule is testable without
  a controller.
- **Win32/**: thin P/Invoke wrappers that make no decisions.
- **Ui/**: theme tokens and themed controls. No business logic.
- **TrayContext / Program**: wire everything together and own the process lifetime.

## Design system

Dark with a blue accent, following the global `ui-design` skill. Every token lives in
`src/CursorCloaker/Ui/Theme.cs`:

| Token | Value | Use |
| --- | --- | --- |
| Background | `#151821` | window background |
| Surface | `#222735` | menus, raised panels (lighter = closer) |
| TextPrimary / Secondary / Disabled | `#E6E9EF` / `#9AA3B5` / `#5E6678` | 12.2 / 5.9 / 2.6 : 1 on Surface |
| Accent | `#5B9AF5` | checkmarks, selection, focus (5.2 : 1 on Surface) |
| Hover / Pressed layer | white 8% / 12% | interaction states as a state layer |
| Hairline | white 10% | borders and separators |
| Font | Segoe UI 10pt | one step above the Windows default, for couch distance |
| Spacing | 4px grid; menu inset 4, item radius 4 inside an 8px menu radius (concentric) |

## Packaging

`scripts/build.ps1` runs the tests, builds Release into `dist\CursorCloaker\` (exe + config only)
and zips `dist\CursorCloaker-v<version>.zip`. The version comes from `Directory.Build.props`,
the only place it's written. CI (`.github/workflows/ci.yml`) runs the same script on
`windows-latest` and uploads the zip as an artifact. Builds are unsigned.

## Testing

- `dotnet test`: xUnit tests for `Core/`, currently settings parsing and storage.
- UI: rendered and looked at with `scripts/preview-menu.ps1` (screenshot of the real menu).
- Not automated: anything needing a real controller, mouse or fullscreen window. HANDOFF.md lists
  what has been checked by hand.

## Project structure

```
src/CursorCloaker/{Core,Win32,Ui}   app
tests/CursorCloaker.Tests           unit tests
scripts/                            build, menu preview, icon generation
graphics/                           icon artwork (SVG master)
docs/DESIGN.md                      behaviour spec
```

## Conventions

- Comments explain *what* and *why*. Every file has a header comment.
- No colour, font or spacing literal outside `Ui/Theme.cs`.
- Any code path that can swap the cursor must have a matching restore on every exit path.
- Commits: `type: imperative summary` (feat, fix, refactor, docs, test, chore).
