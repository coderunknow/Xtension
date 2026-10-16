# Xtension

Xtension is a small Windows tray app for Brave. It shows one custom **×** on the selected horizontal tab, so you do not close a tab by accident. The × moves when you change tabs. It appears only when Xtension can confirm which tab is active.

> **Status: prerelease `v0.1.0-dev4`.** This candidate adds real-time scan triggering (a tab switch no longer waits
> for the scan interval), performance profiles, appearance settings, validated settings backup and restore, a browser
> provider boundary, richer diagnostics, a real multi-size application icon, and a whole-repository audit in
> [`docs/DEV4_AUDIT.md`](docs/DEV4_AUDIT.md). Windows CI runs static checks, core tests, synthetic Windows tests,
> real-Brave smoke with both EXEs, packaging checks and a published-baseline resource comparison. This is not a
> guarantee across all Brave versions or desktop configurations. See [Verification](#verification) and the Releases
> page for publication state.

## Requirements

- Windows 10 or 11, x86 or x64.
- Brave (Chromium) with horizontal tabs.
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
| Performance profile | `balanced`, `fast`, `low-resource`, `custom` | `balanced` |
| Browsers to watch | Brave is supported; other listed browsers are disabled and are not claimed to work | Brave |
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

Settings → Advanced → **Export** writes `Xtension-settings-<version>.json`: the schema version plus the settings the
app understands. **Import** reads a file of at most 256 KiB, validates the schema and every value, applies what it
accepts, reports by name what it rejected, clamped or preserved, and keeps unknown keys exactly as the normal settings
file does. A backup carries no titles, URLs, log content or machine identity. If applying the values fails, the
previous file is restored and the settings are reloaded, so a failed import cannot leave you with half a configuration.

Out-of-range or unreadable values are replaced by the documented default rather than being trusted. **Reset to defaults** restores every value above (the start-with-Windows entry is kept as it is, because it lives in the registry).

Settings are stored at `%LOCALAPPDATA%\Xtension\settings.ini` as `key=value` lines. Keys this version does not know are preserved when the file is rewritten. Keyboard shortcuts in the windows: `Esc` closes them, and closing the dashboard keeps Xtension running in the notification area.

Native close buttons: if Brave's own "Always hide tab close button" setting is on (it is not confirmed in every Brave version), native close buttons stay hidden. Turning off the × or exiting Xtension does **not** change that Brave setting, so native close buttons may stay hidden until you change it in Brave's settings.

## Browsers

Xtension scans a window only when the foreground process image name matches a browser whose tab-access contract is
implemented **and** which you have enabled. Chrome, Edge and Firefox are listed with their executable names, but their
contract is not implemented for this release: they are marked `unsupported`, their scan path refuses with an explicit
reason (the dashboard says so) and nothing is inferred from the fact that some of them are Chromium-based. `brave.exe`
is the only browser with acceptance evidence for this release.

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
- Chrome, Edge and Firefox are not supported by this release: they are listed for discovery, but their scan path
  refuses rather than guessing, so the × stays hidden when they are in front.
- The brief audio distortion reported when a notification appears has **not** been reproduced in this environment. The
  notification is now Xtension's own non-activating, sound-free window instead of a Shell balloon, which removes the
  mechanisms a balloon can use to disturb playback, but the effect on real audio playback is **not verified**. See
  [`docs/DEV4_AUDIT.md`](docs/DEV4_AUDIT.md).

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
| Real Brave smoke | **Passed** with v1.97.56 for dev2 and dev3. The dev4 job additionally reads the shipped EXE's startup line as icon evidence, measures pointer-entry-to-visible latency (failing above 3000 ms) and records ten real-browser scan samples as p50/p95/max. |
| Published dev3 vs dev4 candidate resources | One CI job measures both EXEs on the same runner and records startup time, idle CPU, working set, handles, UI Automation scan cost and the candidate's own statistics line in `performance.txt`. |
| Application icon | Frames, sizes, bit depth and offsets are checked statically; the Windows harness loads the embedded resource and reports the frame count. Visual appearance at 100%/125%/150%/200% tray scaling is **not verified**. |
| Audio crackle | **Not verified.** No audio device or Windows desktop was available; the notice-window change is a candidate fix, not a measured one. |
| Full desktop acceptance | **Incomplete.** Physical DPI/multiple monitors, rapid-click race stress, tray/startup/graceful exit and unsupported arrangements still need manual coverage. |

Release notes state which items were verified for each release. The dev2 published EXEs were also downloaded, checksum-verified and exercised in [Windows CI](https://github.com/coderunknow/Xtension/actions/runs/38579364444), not just rebuilt from source.

## Development

- `AGENTS.md`: rules for contributors and AI agents.
- `DESIGN_LOG.md`, `AGENTS_LOG.md`: append-only decision and work logs.
- `CHANGELOG.md`: user-visible changes.

## License

MIT. See `LICENSE`.

### Diagnosing a hidden button

Diagnostics distinguishes current hidden state from the last scan. For a scan error, the details include the failing operation, exception type, HRESULT and application method frames. Provider messages are intentionally omitted because they can contain private browsing data. Do not share tab-list screenshots without redacting titles. A stale/incomplete/ambiguous scan never authorizes Ctrl+W. See [dev4 audit](docs/DEV4_AUDIT.md) for this candidate's findings, fixes, exact blockers and remaining gaps,
[dev3 audit](docs/DEV3_AUDIT.md) for the previous candidate, and [dev2 audit](docs/DEV2_AUDIT.md) for the release before
that.
