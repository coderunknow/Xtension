# Xtension

Xtension is a small Windows tray app for supported browsers. It shows one custom **×** on the selected horizontal tab, so you do not close a tab by accident. The × moves when you change tabs. It appears only when Xtension can confirm which tab is active.

> **Status: `v0.1.0-dev5` candidate — release gates in progress.** This version strengthens exact-tab closing,
> transactional settings and event recovery, adds forward-compatible backups, and adds independently tested opt-in
> Chrome alongside Brave. Edge failed acceptance and is explicitly disabled. See [`docs/DEV5_AUDIT.md`](docs/DEV5_AUDIT.md) for actual acceptance state and remaining
> limitations. Source and a build are not proof of GUI behavior. Publication is confirmed only on the
> [Releases page](https://github.com/coderunknow/Xtension/releases).

## Requirements

- Windows 10 or 11, x86 or x64.
- A supported browser with horizontal tabs; Brave is enabled by default. Chrome requires explicit opt-in; Edge and Firefox are unsupported.
- .NET Framework 4.8 runtime.
- No administrator rights. Xtension runs as your user.

## Install

Download `Xtension-v0.1.0-dev4-win-x64.exe` (or `-win-x86.exe` for 32-bit Windows) from the [Releases](https://github.com/coderunknow/Xtension/releases) page. Check it against `SHA256SUMS.txt`:

```powershell
Get-FileHash .\Xtension-v0.1.0-dev4-win-x64.exe -Algorithm SHA256
```

Run it. A tray icon appears. Open the dashboard from the tray menu.

## Use

- The dashboard shows whether the × is active, the tab Xtension has confirmed, and the reason if the × is hidden.
- **Settings** opens the Xtension settings window (also in the tray menu). It contains every option below.
- **Turn off the ×** from the tray menu or dashboard. This only hides the ×.
- **Exit** from the tray menu. This closes Xtension.
- **Start with Windows** is off by default. Turn it on from the tray menu, dashboard or settings window.
- Diagnostics (tray menu → *Chẩn đoán*) separates the current state from the last scan result, names the precise
  reason when the × is hidden, shows the active profile, the effective configuration and the scan statistics (counts,
  durations, queue time, event-to-visible time), and offers a copyable, sanitized report.
- **Settings → Advanced** exports the settings to a file and imports one back. An import is validated and bounded
  before anything is written, reports which values were applied, rejected or preserved, and restores the previous file
  if the write fails.

### Settings

Open Settings from the tray or dashboard for the options below. Changes apply immediately and persist; the tray also offers enable/disable and startup actions.

| Setting | Values | Default |
| --- | --- | --- |
| Show the × | on / off | on |
| Visibility mode | `always` – show whenever the active tab is confirmed; `hover` – show only while the pointer is over the confirmed selected tab (or on the button) | `always` |
| × size | 18–40 logical pixels | 26 |
| Right inset | 0–12 logical pixels from the tab's right edge | 3 |
| Hover keep-time | 0–1000 ms after the pointer leaves the tab, so the pointer can travel from tab to button | 250 ms |
| Scan interval | 300–2000 ms between UI Automation scans while an enabled supported browser is in front | 500 ms |
| Interface language | Tiếng Việt / English | Tiếng Việt |
| Performance profile | `balanced`, `fast`, `low-resource`, `custom` | `balanced` |
| Browsers to watch | Brave; opt-in Chrome; Edge/Firefox unsupported | Brave |
| × colour | `dark`, `red`, `blue`, `green` | `dark` |
| × border | on / off | on |
| × opacity | 30–100%, applied as uniform window alpha so the hit area never changes | 100% |

The whole × square plus its right inset must fit inside the selected tab. If the configured combination cannot, the
button stays hidden and the dashboard says why ("The selected tab is too small to hold the × button safely at the
configured size. The × is hidden.") instead of drawing a button outside the tab. How much room a tab has comes from
the browser: on the CI runner a Brave v1.97.56 tab holds the 40 px maximum with a 5 px inset, and a tab that cannot
hold the configured combination hides the button instead.

The settings window is grouped into **General**, **Appearance**, **Performance** and **Advanced**, and every control
shows the value the application is actually using: choosing a profile writes its scan interval into the interval box
immediately, and editing any profile-controlled value switches the profile to `custom` on the spot instead of keeping
the preset's name next to a value that is no longer the preset's. Every label exists in both languages and follows
text scaling and a narrow window.

### Performance profiles

A profile is a starting point, not a hidden rule. `balanced` is the default and equals the previous release's
cadences, so an existing installation behaves the same until you choose otherwise.

| Profile | Scan interval | Foreground poll | Hover poll | Dashboard scan |
| --- | --- | --- | --- | --- |
| `balanced` | 500 ms | 250 ms | 100 ms | 2000 ms |
| `fast` | 300 ms | 100 ms | 50 ms | 1500 ms |
| `low-resource` | 1200 ms | 500 ms | 250 ms | 5000 ms |
| `custom` | whatever you set | 250 ms | 100 ms | 2000 ms |

The individual keys stay authoritative: the profile marker only records which preset you last chose, and a value you
edit yourself is kept across a restart and reported as `custom`. **No profile can change a scan budget, a deadline, the
staleness window, the stall threshold, the close cooldown or any confirmation rule** — those are not settings, by
design, because they decide whether a close is authorised.

### Backup and restore

Settings → Advanced → **Export** writes `xtension-settings.ini`: a bounded UTF-8 `key=value` document with schema metadata, known settings and unknown/future keys. It is not JSON. **Import** reads a file of at most 256 KiB, validates the schema and every value, applies what it
accepts, reports by name what it rejected, clamped or preserved, and keeps unknown keys exactly as the normal settings
file does. Xtension collects no browsing data or logs for a backup. Unknown keys may contain user-authored custom values; inspect before sharing. A failed write leaves both the previous file and the store cache unchanged.

Numeric values outside the range are clamped; unreadable values use documented defaults rather than being trusted. Rejected backup values use defaults too, and are reported. **Reset to defaults** restores every value above (the start-with-Windows entry is kept as it is, because it lives in the registry).

Settings are stored at `%LOCALAPPDATA%\Xtension\settings.ini` as `key=value` lines. Keys this version does not know are preserved when the file is rewritten. Keyboard shortcuts in the windows: `Esc` closes them, and closing the dashboard keeps Xtension running in the notification area.

Native close buttons: if Brave's own "Always hide tab close button" setting is on (it is not confirmed in every Brave version), native close buttons stay hidden. Turning off the × or exiting Xtension does **not** change that Brave setting, so native close buttons may stay hidden until you change it in Brave's settings.

## Browsers

Xtension scans only an exact known foreground process image whose provider is implemented and which you enabled.
Brave and Chrome have independently exercised contracts using direct UIA Tab → TabItem parentage, known unique
SelectionItem selection, runtime identity and horizontal geometry. Chrome passed actual EXE acceptance in both
architectures. **Edge is unsupported:** its attempted acceptance reproducibly exhausted the unchanged depth cap after
restart, so its provider is disabled and cannot be opted into. CI verifies detection-only refusal against real Edge,
not support. Firefox's Gecko provider remains unimplemented. See the dev5 audit for versions, failed runs and evidence.

Existing settings and new defaults remain **Brave-only** (`browsers=brave`). Enable additional browsers in Settings;
clearing a browser disables placement and closing there. Native browser close buttons are not hidden or changed by
Xtension. The Brave appearance-settings shortcut is deliberately Brave-specific, not a Chrome/Edge settings control.

## How it stays safe

- The × sends **Ctrl+W** only after a fresh UI Automation scan confirms that the click landed on the × of the one selected horizontal tab, the fresh runtime identity matches the displayed target, and the enabled browser is still foreground.
- If the scan is incomplete, ambiguous, slow, or the window moved, the × is hidden and the click does nothing.
- Ctrl+W is not sent while Shift, Alt, or Win is held, because Ctrl+Shift+W would close the whole window.
- The × window never takes focus from the browser.
- In `hover` mode the button appears only over the confirmed selected tab, so a stray click on the tab strip cannot reach it; clicking it still goes through the same fresh validation before any keystroke.
- The × appears as soon as the pointer reaches the tab strip: arriving there asks for a scan immediately instead of
  waiting out the rest of the scan interval, and while the pointer is inside the strip `hover` mode scans at the
  documented minimum interval. Scanning stops again while the pointer is elsewhere, so responsiveness costs nothing
  when the pointer is not on a tab.
- Scans run on background threads. A hung provider does not freeze the tray; stale confirmation hides the ×; after 5 seconds diagnostics reports a stall.
- Logs contain no tab or window titles.

## Known limitations

- Windows only. No macOS or Linux support.
- Depends on each browser's accessibility tree. A browser update that changes it causes the × to hide, not to guess.
- A tab switch in the short gap between confirmation and the keystroke cannot be prevented by any available API. This residual risk is recorded in `DESIGN_LOG.md`.
- Vertical tabs and pinned or scrolled-out tabs are not treated as the target.
- Firefox is unsupported. Brave/Chrome support is not a promise about every browser version; actual tested
  versions and architecture results belong in the audit.
- Physical multi-monitor/mixed-DPI behavior, native 32-bit Windows and genuinely hung providers still need relevant
  environments; scaled-font and x86-process CI tests are not substitutes.
- The owner reports the historical audio distortion is **not currently observed**. Its cause was not established;
  no causal fix is claimed. Sound-free, non-activating notices are retained.

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
| Real browsers | Brave and opt-in Chrome passed full packaged-EXE acceptance on x86/x64 (`66da890`, CI `39108878064`); both real-Edge refusal jobs passed. Edge is disabled, not supported. Final PR/main/release gates retest exact source and bytes. |
| Published dev4 vs candidate resources | Three 20-second observations per binary/state, alternating order on one runner; startup, CPU, working/private memory, handles, scan/queue counters. Raw evidence and limitations in the audit; no percentage speedup claim. |
| Application icon | Frames/decoding are tested; human visual appearance at scaled tray sizes is **not verified**. |
| Audio | Owner reports historical distortion is **not currently observed**; causal root cause remains unconfirmed. |
| Full desktop acceptance | **Incomplete.** Physical mixed-DPI/multi-monitor, native x86 OS, true provider hangs, tray/startup/graceful exit and unsupported layouts remain limited. Rapid selection preservation and held-press cancellation have automated coverage, not exhaustive race certification. |

Release notes state which items were verified for each release. The dev2 published EXEs were also downloaded, checksum-verified and exercised in [Windows CI](https://github.com/coderunknow/Xtension/actions/runs/38579364444), not just rebuilt from source.

## Development

- `AGENTS.md`: rules for contributors and AI agents.
- `DESIGN_LOG.md`, `AGENTS_LOG.md`: append-only decision and work logs.
- `CHANGELOG.md`: user-visible changes.

## License

MIT. See `LICENSE`.

### Diagnosing a hidden button

Diagnostics distinguishes current hidden state from the last scan. For a scan error, the details include the failing operation, exception type, HRESULT and application method frames. Provider messages are intentionally omitted because they can contain private browsing data. Do not share tab-list screenshots without redacting titles. A stale/incomplete/ambiguous scan never authorizes Ctrl+W. See [dev5 audit](docs/DEV5_AUDIT.md) for this candidate's findings, fixes, exact blockers and remaining gaps,
[dev4 audit](docs/DEV4_AUDIT.md) for the previous release, and the earlier per-release audits for history.
