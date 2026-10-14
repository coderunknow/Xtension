# Xtension

Xtension is a small Windows tray app for Brave. It shows one custom **×** on the selected horizontal tab, so you do not close a tab by accident. The × moves when you change tabs. It appears only when Xtension can confirm which tab is active.

> **Status: prerelease `v0.1.0-dev1`.** Source checks and unit tests run in CI. Behaviour inside Brave has not been verified on a real installation. See [Verification](#verification).

## Requirements

- Windows 10 or 11, x86 or x64.
- Brave (Chromium) with horizontal tabs.
- No administrator rights. Xtension runs as your user.

## Install

Download `Xtension-v0.1.0-dev1-win-x64.exe` (or `-win-x86.exe` for 32-bit Windows) from the [Releases](https://github.com/coderunknow/Xtension/releases) page. Check it against `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Xtension-v0.1.0-dev1-win-x64.exe -Algorithm SHA256
```

Run it. A tray icon appears. Open the dashboard from the tray menu.

## Use

- The dashboard shows whether the × is active, the tab Xtension has confirmed, and the reason if the × is hidden.
- **Turn off the ×** from the tray menu or dashboard. This only hides the ×.
- **Exit** from the tray menu. This closes Xtension.
- **Start with Windows** is off by default. Turn it on from the tray menu or dashboard.
- Diagnostics (tray menu → *Chẩn đoán*) lists the tabs from the last scan.

Native close buttons: if Brave's own "Always hide tab close button" setting is on (it is not confirmed in every Brave version), native close buttons stay hidden. Turning off the × or exiting Xtension does **not** change that Brave setting, so native close buttons may stay hidden until you change it in Brave's settings.

## How it stays safe

- The × sends **Ctrl+W** only after a fresh UI Automation scan confirms that the click landed on the × of the one selected horizontal tab, and Brave is still the foreground window.
- If the scan is incomplete, ambiguous, slow, or the window moved, the × is hidden and the click does nothing.
- Ctrl+W is not sent while Shift, Alt, or Win is held, because Ctrl+Shift+W would close the whole window.
- The × window never takes focus from Brave.
- Scans run on a background thread. A hung Brave does not freeze the tray; after 5 seconds the × is hidden.
- Logs contain no tab or window titles.

## Known limitations

- Windows only. No macOS or Linux support.
- Depends on Brave's accessibility tree. A Brave update that changes it causes the × to hide, not to guess.
- A tab switch in the short gap between confirmation and the keystroke cannot be prevented by any available API. This residual risk is recorded in `DESIGN_LOG.md`.
- Vertical tabs and pinned or scrolled-out tabs are not treated as the target.

## Build and test

Requires Windows PowerShell or PowerShell 7 and the .NET Framework 4.8 Developer Pack or Build Tools.

```powershell
./scripts/verify-source.ps1      # static checks
./scripts/test.ps1               # unit tests for the pure core
./scripts/build.ps1 -Arch x64    # dist/Xtension-win-x64.exe
./scripts/build.ps1 -Arch x86    # dist/Xtension-win-x86.exe
```

The version comes only from `VERSION`. Edit that file to change it.

## Logs

`%LOCALAPPDATA%\Xtension\app.log` (rotated at 1 MiB). Settings are in `settings.ini` in the same folder.

## Verification

| Area | Status |
| --- | --- |
| Static checks (`verify-source.ps1`) | Run in CI on each push and pull request. |
| Unit tests (pure core) | Run in CI. |
| x86 and x64 builds | Run in CI. Artifacts are attached to the release. |
| Behaviour in Brave (×, click, close, DPI, startup, tray) | **Not verified.** Requires manual testing on Windows with Brave. |

Release notes state which items were verified for each release.

## Development

- `AGENTS.md`: rules for contributors and AI agents.
- `DESIGN_LOG.md`, `AGENTS_LOG.md`: append-only decision and work logs.
- `CHANGELOG.md`: user-visible changes.

## License

MIT. See `LICENSE`.
