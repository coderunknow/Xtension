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

## Commands (run in PowerShell on Windows)

```powershell
./scripts/verify-source.ps1        # static checks, no compiler needed
./scripts/test.ps1                 # compile and run unit tests (pure core only)
./scripts/build.ps1 -Arch x86      # dist/Xtension-win-x86.exe
./scripts/build.ps1 -Arch x64      # dist/Xtension-win-x64.exe
msbuild Xtension.csproj /restore /p:Configuration=Release /p:Platform=x64
./scripts/package.ps1              # dist/release/* (needs both builds and a clean git tree)
```

## Architecture map

| Path | Role |
| --- | --- |
| `src/Core` | Pure logic: geometry, tab selection, close guard, clock, settings, logging. Unit-tested. |
| `src/Interop` | Win32 P/Invoke, process identity, `SendInput` for Ctrl+W. |
| `src/Uia` | Bounded, read-only UI Automation walk of a Brave window. |
| `src/App` | State machine (`ActiveTabController`), worker thread (`ScanWorker`), tray context, single instance, startup entry. |
| `src/UI` | Overlay window, dashboard, diagnostics, tray icon drawing. |
| `tests/UnitTests` | Minimal test runner with `[Test]` attributes. No external test framework. |
| `scripts/` | Build, test, verify, package, and version generation. |
| `.github/workflows` | CI (static checks, tests, builds, MSBuild check) and tag-triggered prerelease. |

## Before you change behaviour

- Read the relevant entries in `DESIGN_LOG.md`.
- Keep the safety model: the overlay's click path must re-check foreground and epoch, and must call `CloseGuard.Evaluate` before any keystroke.
- Add or update a unit test for pure-logic changes. UI and Brave behaviour cannot be unit-tested here; say so in the PR.
- Record what you did and what you verified in `AGENTS_LOG.md`.

## Verification language

Report results in four separate groups: static checks, automated tests, CI, and GUI/Brave runtime. Do not present a successful compile or static check as proof of GUI behaviour.
