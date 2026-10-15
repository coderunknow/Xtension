# AGENTS.md — instructions for AI and human contributors

## What this project is

Xtension is a Windows-only tray utility. It shows one custom × on the selected horizontal tab of a Brave window, so tabs are not closed by accident. It is a .NET Framework 4.8 WinForms app with no NuGet dependencies.

Platform scope: Windows x86 and x64 only. Do not claim macOS or Linux support, and do not add placeholder binaries.

## Non-negotiable rules

1. **Fail closed.** The × may send Ctrl+W only after a fresh UI Automation scan confirms exactly one selected horizontal tab, the click lands on that tab's ×, and Brave is still the foreground window. Any doubt hides the button.
2. **C# 5 only.** The compiler is .NET Framework csc with `/langversion:5`. Do not use `?.`, `$""`, `nameof`, `out var`, `when` filters, expression-bodied members, or getter-only auto-properties. `scripts/verify-source.ps1` checks the common cases.
3. **One version source.** Edit `VERSION` only. `scripts/update-version.ps1` generates `build/generated/VersionInfo.g.cs` and `build/generated/app.manifest`. Never hard-code a version in source, docs, or the manifest template.
4. **No private data in logs.** Never log tab titles, window titles, or URLs. `AppLog` must not reference titles. Diagnostics shows titles in the UI only.
5. **PowerShell scripts stay ASCII.** Windows PowerShell 5.1 reads BOM-less scripts as ANSI. Put non-ASCII text in Markdown or resource files.
6. **Append-only logs.** `DESIGN_LOG.md` and `AGENTS_LOG.md` are append-only. Corrections are new dated entries. Do not rewrite or delete old entries. Quoted user requests stay verbatim.
7. **No secrets.** Never commit tokens, keys, `.pfx`, `.snk`, `settings.ini`, or logs. `verify-source.ps1` scans tracked and untracked files for common token patterns.
8. **Keep the MIT license.** Do not edit `LICENSE`.
9. **Explicit compile list.** Every file under `src/` must appear in `Xtension.csproj`. `verify-source.ps1` enforces this.
10. **User-visible text lives in one table.** Every user-facing string is a `StringId` in `src/Core/Strings.cs` with a Vietnamese and an English row; UI, app and interop code must not contain Vietnamese text, and `verify-source.ps1` fails if it does. Log lines and internal decision reasons stay English on purpose: they are diagnostic, not interface.
11. **Settings go through one path.** Values live in `AppSettings` (range constants in `AppInfo`), are clamped by `Normalize()`, and are applied by the application so the controller, overlay, tray menu, dashboard and settings window cannot disagree. The settings window talks to the application only through `ISettingsHost`; a setting that only one window knows about is a bug.
12. **Hover is a display rule, never an authorisation.** Pointer position may only decide whether the button is visible. Closing always re-validates with a fresh scan through `CloseGuard`.
13. **Speed may not lower the bar.** Optimise only costs you can point at, and never remove a freshness check, deadline, budget, epoch revalidation, stall rule or confirmation step to gain CPU.

## Commands (run in PowerShell on Windows)

```powershell
./scripts/verify-source.ps1        # static checks, no compiler needed
./scripts/test.ps1                 # compile and run unit tests (pure core only)
./scripts/build.ps1 -Arch x86      # dist/Xtension-win-x86.exe
./scripts/build.ps1 -Arch x64      # dist/Xtension-win-x64.exe
msbuild Xtension.csproj /restore /p:Configuration=Release /p:Platform=x64
./scripts/package.ps1              # dist/release/* (needs both builds and a clean git tree)
./scripts/measure-performance.ps1 -Baseline <published.exe> -Candidate dist/Xtension-win-x64.exe  # CI only
```

`tools/api-check.py` and `tools/verify-source.py` are local development aids for environments without a compiler or
PowerShell (identifier/string-table cross-check and a mirror of the static checks). They are not part of the build,
are not shipped, and passing them is not evidence that the project compiles.

## Architecture map

| Path | Role |
| --- | --- |
| `src/Core` | Pure logic: geometry, tab selection, close guard, hover policy, typed settings, string table, clock, logging. Unit-tested. |
| `src/Interop` | Win32 P/Invoke, pointer position read, process identity, `SendInput` for Ctrl+W. No pointer injection. |
| `src/Uia` | Bounded, read-only UI Automation walk of a Brave window. |
| `src/App` | State machine (`ActiveTabController`), worker threads (`ScanWorker`), tray context (`TrayAppContext`, also the `ISettingsHost` implementation), single instance, startup entry. |
| `src/UI` | Overlay window, dashboard (`MainForm`), settings (`SettingsForm`, `ISettingsHost`), diagnostics, layout helpers, tray icon drawing. |
| `tests/UnitTests` | Minimal test runner with `[Test]` attributes. No external test framework. |
| `tests/WindowsTests` | Synthetic UI Automation/layout/settings/injection tests and the resource measurement mode, plus the real-Brave acceptance phase. |
| `scripts/` | Build, test, verify, package, version generation, and the CI-only resource measurement. |
| `docs/` | Release capability boundary, and one audit document per release with findings and remaining risk. |
| `.github/workflows` | CI (static checks, tests, both builds, MSBuild, packaging, resource comparison, real-Brave smoke) and tag-triggered prerelease. |

## Before you change behaviour

- Read the relevant entries in `DESIGN_LOG.md`.
- Keep the safety model: the overlay's click path must re-check foreground and epoch, and must call `CloseGuard.Evaluate` before any keystroke.
- Add or update a unit test for pure-logic changes. UI and Brave behaviour cannot be unit-tested here; say so in the PR.
- Record what you did and what you verified in `AGENTS_LOG.md`.
- When you add a setting, add it to `AppSettings`, `SettingsStore`, the string table, the settings window, the README
  table, and a core test. A setting that is only in one of those places is incomplete.

## Verification language

Report results in four separate groups: static checks, automated tests, CI, and GUI/Brave runtime. Do not present a successful compile or static check as proof of GUI behaviour.
