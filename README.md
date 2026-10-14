# Xtension

Xtension is a small Windows tray app for Brave. It shows one custom **×** on the selected horizontal tab, so you do not close a tab by accident. The × moves when you change tabs. It appears only when Xtension can confirm which tab is active.

> **Status: prerelease `v0.1.0-dev2`.** The UIA cache failure is corrected and Windows CI includes real-Brave smoke tests with both EXEs. This is not a guarantee across all Brave versions or desktop configurations. See [Verification](#verification) and the Releases page for publication state.

## Requirements

- Windows 10 or 11, x86 or x64.
- Brave (Chromium) with horizontal tabs.
- .NET Framework 4.8 runtime.
- No administrator rights. Xtension runs as your user.

## Install

Download `Xtension-v0.1.0-dev2-win-x64.exe` (or `-win-x86.exe` for 32-bit Windows) from the [Releases](https://github.com/coderunknow/Xtension/releases) page. Check it against `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Xtension-v0.1.0-dev2-win-x64.exe -Algorithm SHA256
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
- Scans run on a background thread. A hung Brave does not freeze the tray; stale confirmation hides the ×; after 5 seconds diagnostics reports a stall.
- Logs contain no tab or window titles.

## Known limitations

- Windows only. No macOS or Linux support.
- Depends on Brave's accessibility tree. A Brave update that changes it causes the × to hide, not to guess.
- A tab switch in the short gap between confirmation and the keystroke cannot be prevented by any available API. This residual risk is recorded in `DESIGN_LOG.md`.
- Vertical tabs and pinned or scrolled-out tabs are not treated as the target.

## Build and test

Requires Windows PowerShell 5.1 or PowerShell 7 and the .NET Framework 4.8 Developer Pack or Build Tools. The CI-only `test-brave.ps1` uses a disposable browser profile and refuses personal desktops.

```powershell
./scripts/verify-source.ps1      # static checks
./scripts/test.ps1               # unit tests for the pure core
./scripts/test-windows.ps1 -Arch x64 # synthetic Windows UIA/layout checks (not Brave)
./scripts/build.ps1 -Arch x64    # dist/Xtension-win-x64.exe
./scripts/build.ps1 -Arch x86    # dist/Xtension-win-x86.exe
```

The version comes only from `VERSION`. Edit that file to change it.

## Logs

`%LOCALAPPDATA%\Xtension\app.log` (rotated at 1 MiB). Settings are in `settings.ini` in the same folder.

## Verification

| Area | Status |
| --- | --- |
| Static checks (`verify-source.ps1`) | Run in CI on main pushes and pull requests. |
| Unit tests (pure core) | Run in CI. |
| x86 and x64 builds | Run in CI. Verified in CI; release builds are produced by the tag workflow. |
| Windows UIA fixture / scaled-font layout | Run in CI in both architectures; not real-browser or physical-monitor validation. |
| Real Brave smoke | **Passed** with v1.97.56, x86/x64 EXEs, Windows runner at 96 DPI: overlay, switches, multiple windows, focus changes, modifiers, exact selected-tab close, window geometry, many tabs and long title. |
| Full desktop acceptance | **Incomplete.** Physical DPI/multiple monitors, rapid-click race stress, tray/startup/graceful exit and unsupported arrangements still need manual coverage. |

Release notes state which items were verified for each release. The dev2 published EXEs were also downloaded, checksum-verified and exercised in [Windows CI](https://github.com/coderunknow/Xtension/actions/runs/38579364444), not just rebuilt from source.

## Development

- `AGENTS.md`: rules for contributors and AI agents.
- `DESIGN_LOG.md`, `AGENTS_LOG.md`: append-only decision and work logs.
- `CHANGELOG.md`: user-visible changes.

## License

MIT. See `LICENSE`.

### Diagnosing a hidden button

Diagnostics distinguishes current hidden state from the last scan. For a scan error, the details include the failing operation, exception type, HRESULT and application method frames. Provider messages are intentionally omitted because they can contain private browsing data. Do not share tab-list screenshots without redacting titles. A stale/incomplete/ambiguous scan never authorizes Ctrl+W. See [dev2 audit](docs/DEV2_AUDIT.md) for findings and acceptance gaps.
