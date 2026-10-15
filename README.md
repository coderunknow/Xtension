# Xtension

Xtension is a small Windows tray app for Brave. It shows one custom **×** on the selected horizontal tab, so you do not close a tab by accident. The × moves when you change tabs. It appears only when Xtension can confirm which tab is active.

> **Status: prerelease `v0.1.0-dev3`.** This candidate adds a settings window, a hover visibility mode, a configurable × size, a Vietnamese/English language toggle and measured resource work. Windows CI runs static checks, core tests, synthetic Windows tests, real-Brave smoke with both EXEs, packaging checks and a published-baseline resource comparison. This is not a guarantee across all Brave versions or desktop configurations. See [Verification](#verification) and the Releases page for publication state.

## Requirements

- Windows 10 or 11, x86 or x64.
- Brave (Chromium) with horizontal tabs.
- .NET Framework 4.8 runtime.
- No administrator rights. Xtension runs as your user.

## Install

Download `Xtension-v0.1.0-dev3-win-x64.exe` (or `-win-x86.exe` for 32-bit Windows) from the [Releases](https://github.com/coderunknow/Xtension/releases) page. Check it against `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Xtension-v0.1.0-dev3-win-x64.exe -Algorithm SHA256
```

Run it. A tray icon appears. Open the dashboard from the tray menu.

## Use

- The dashboard shows whether the × is active, the tab Xtension has confirmed, and the reason if the × is hidden.
- **Settings** opens the Xtension settings window (also in the tray menu). It contains every option below.
- **Turn off the ×** from the tray menu or dashboard. This only hides the ×.
- **Exit** from the tray menu. This closes Xtension.
- **Start with Windows** is off by default. Turn it on from the tray menu, dashboard or settings window.
- Diagnostics (tray menu → *Chẩn đoán*) lists the tabs from the last scan.

### Settings

Every option is also available from the tray menu, and every change applies immediately to the running app.

| Setting | Values | Default |
| --- | --- | --- |
| Show the × | on / off | on |
| Visibility mode | `always` – show whenever the active tab is confirmed; `hover` – show only while the pointer is over the confirmed selected tab (or on the button) | `always` |
| × size | 18–40 logical pixels | 26 |
| Right inset | 0–12 logical pixels from the tab's right edge | 3 |
| Hover keep-time | 0–1000 ms after the pointer leaves the tab, so the pointer can travel from tab to button | 250 ms |
| Scan interval | 300–2000 ms between UI Automation scans while Brave is in front | 500 ms |
| Interface language | Tiếng Việt / English | Tiếng Việt |

The whole × square plus its right inset must fit inside the selected tab. If the configured combination cannot, the
button stays hidden and the dashboard says why ("The selected tab is too small to hold the × button safely at the
configured size. The × is hidden.") instead of drawing a button outside the tab. How much room a tab has comes from
the browser: on the CI runner a Brave v1.97.56 tab holds the 40 px maximum with a 5 px inset, and a tab that cannot
hold the configured combination hides the button instead.

Out-of-range or unreadable values are replaced by the documented default rather than being trusted. **Reset to defaults** restores every value above (the start-with-Windows entry is kept as it is, because it lives in the registry).

Settings are stored at `%LOCALAPPDATA%\Xtension\settings.ini` as `key=value` lines. Keys this version does not know are preserved when the file is rewritten. Keyboard shortcuts in the windows: `Esc` closes them, and closing the dashboard keeps Xtension running in the notification area.

Native close buttons: if Brave's own "Always hide tab close button" setting is on (it is not confirmed in every Brave version), native close buttons stay hidden. Turning off the × or exiting Xtension does **not** change that Brave setting, so native close buttons may stay hidden until you change it in Brave's settings.

## How it stays safe

- The × sends **Ctrl+W** only after a fresh UI Automation scan confirms that the click landed on the × of the one selected horizontal tab, and Brave is still the foreground window.
- If the scan is incomplete, ambiguous, slow, or the window moved, the × is hidden and the click does nothing.
- Ctrl+W is not sent while Shift, Alt, or Win is held, because Ctrl+Shift+W would close the whole window.
- The × window never takes focus from Brave.
- In `hover` mode the button appears only over the confirmed selected tab, so a stray click on the tab strip cannot reach it; clicking it still goes through the same fresh validation before any keystroke.
- The × appears as soon as the pointer reaches the tab strip: arriving there asks for a scan immediately instead of
  waiting out the rest of the scan interval, and while the pointer is inside the strip `hover` mode scans at the
  documented minimum interval. Scanning stops again while the pointer is elsewhere, so responsiveness costs nothing
  when the pointer is not on a tab.
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
| Windows UIA fixture / scaled-font layout | Run in CI in both architectures; not real-browser or physical-monitor validation. Includes settings-window layout at 100/125/150/200% text scaling, the language toggle, overlay resize and a delivered Ctrl+W chord. |
| Real Brave smoke | **Passed** with v1.97.56 for dev2. The dev3 job tests hover mode, the largest configured × size the real tab can contain, the documented fail-closed refusal of a size that cannot fit, settings persistence across a restart and corrupted-settings recovery. |
| Published dev2 vs candidate resources | One CI job measures both EXEs on the same runner and records startup time, idle CPU, working set, handles and UI Automation scan cost in `performance.txt`. |
| Full desktop acceptance | **Incomplete.** Physical DPI/multiple monitors, rapid-click race stress, tray/startup/graceful exit and unsupported arrangements still need manual coverage. |

Release notes state which items were verified for each release. The dev2 published EXEs were also downloaded, checksum-verified and exercised in [Windows CI](https://github.com/coderunknow/Xtension/actions/runs/38579364444), not just rebuilt from source.

## Development

- `AGENTS.md`: rules for contributors and AI agents.
- `DESIGN_LOG.md`, `AGENTS_LOG.md`: append-only decision and work logs.
- `CHANGELOG.md`: user-visible changes.

## License

MIT. See `LICENSE`.

### Diagnosing a hidden button

Diagnostics distinguishes current hidden state from the last scan. For a scan error, the details include the failing operation, exception type, HRESULT and application method frames. Provider messages are intentionally omitted because they can contain private browsing data. Do not share tab-list screenshots without redacting titles. A stale/incomplete/ambiguous scan never authorizes Ctrl+W. See [dev3 audit](docs/DEV3_AUDIT.md) for this candidate's findings, fixes and remaining gaps, and [dev2 audit](docs/DEV2_AUDIT.md) for the previous release.
