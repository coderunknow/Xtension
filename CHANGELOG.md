# Changelog

All notable changes are listed here. Versions follow SemVer. The version in `VERSION` is the single source.

## [0.1.0-dev3] - 2026-10-15

Prerelease. Windows x86/x64, .NET Framework 4.8, C# 5. No dependency added. No published tag or asset was replaced.

### Added

- **Settings window** (dashboard, tray menu, or `Settings ...`): turn the × on/off, choose the visibility mode, set the × size and right inset, set the hover keep-time and the scan interval, switch the interface language, set start-with-Windows, reset to defaults, open diagnostics or the log.
- **Hover mode** (`visibilityMode=hover`): the × is shown only while the pointer is over the confirmed selected tab (or on the button itself), with a bounded keep-time (default 250 ms) so moving from the tab onto the button does not make it vanish. Inactive tabs, the title bar, the page area, the taskbar and other applications never qualify.
- **Configurable × size and position**: 18–40 logical pixels (default 26) and a 0–12 logical pixel right inset (default 3). Painting, window placement, hit testing and close validation all use the same configured values. A size whose square plus the inset cannot fit inside the selected tab hides the button and reports that the tab is too small for it, rather than drawing a button outside the tab.
- **Interface language toggle**: Vietnamese (default) and English. Every user-visible string moved into one table; changing the language relabels the settings window, dashboard, tray menu and overlay in place, and re-renders the explanation of the last scan without waiting for a new scan.
- Own settings file at `%LOCALAPPDATA%\Xtension\settings.ini` with the keys `overlayEnabled`, `visibilityMode`, `buttonSize`, `buttonInset`, `hoverGraceMs`, `scanIntervalMs`, `language`. Unknown keys are preserved. Start-with-Windows stays a per-user registry entry and is not duplicated into the file.
- `docs/DEV3_AUDIT.md`: findings with severity, what was fixed, and the remaining unverified risks.
- A Windows performance job that measures the published dev2 EXE and the candidate EXE in one job on the same runner (startup time, idle CPU, working set, handles, UI Automation scan cost).

### Fixed

- `SettingsStore` re-read and re-parsed the file on every getter call; it now caches values in memory, bounds the file size it will load, clamps out-of-range integers and unknown enum tokens to documented defaults, preserves unknown keys when rewriting, and removes a stray `settings.ini.tmp` after a failed replacement that previously survived forever.
- The overlay performed `SetWindowPos`, a bounds read and a repaint on every scan even when nothing moved. Placement is now idempotent and only repaints when the size changes, while still verifying the native bounds and visibility whenever it does move the window.
- A non-Brave foreground re-opened a process handle on every 250 ms tick; negative verdicts are now cached per window and process id (shorter lifetime), while the path that sends Ctrl+W always re-queries.
- The audit suspected each scan leaked its `CacheRequest`. The compiler refuted that: in .NET Framework 4.8 `System.Windows.Automation.CacheRequest` has no `Dispose`, and the disposable activation object was already released by the existing `using` block. The scope of the investigation and its conclusion are recorded in `docs/DEV3_AUDIT.md`; no change was needed beyond not adding an impossible call.
- The controller built a snapshot and pushed a render on every timer tick even when nothing observable had changed.
- The dev2 wording edge case where a minimized non-Brave window was described as "Brave minimized" is corrected; the wording now requires a Brave identity.
- In hover mode, UI Automation work is skipped while the pointer cannot be inside the tab strip. This can only delay showing the × by one interval and can never authorise a close.

### Performance

- **The × no longer waits out the scan interval.** In `hover` mode the moment the pointer enters the tab strip forces the
  next scan to run on that tick, and while the pointer is inside the strip the loop runs at the documented minimum
  interval instead of the configured one; in `always` mode a window that newly comes to the front is scanned immediately.
  The steady-state cadence, every deadline, the staleness window and every confirmation rule are unchanged, and no extra
  scan is submitted while the pointer is away from the tab strip.
- **Windows open faster.** The settings window and the dashboard add their whole control tree with layout suspended and
  lay it out once, each wrapping label measures its text once per text/width/font instead of once per layout pass, and
  both windows share a small set of fonts instead of creating one `Font` per control. `FitToWorkingArea` no longer
  resizes, relocates and relayouts a window that already fits the working area.

### Safety

- The close path is unchanged: a click still only sends Ctrl+W after a fresh scan of the current foreground Brave window confirms the click landed on the × of the one selected horizontal tab, blocking modifiers are not held, and the foreground window is re-read immediately before injection.
- Every geometry, mode, language and interval change invalidates pending confirmation, so a queued validation can never be applied to a differently sized or differently placed button.
- A value that arrives from a corrupted file, a hostile caller or the registry is clamped to its documented range and never trusted, and the settings window repairs out-of-range values instead of crashing.

### Regression coverage and verification

- Core tests extended: settings persistence (round trip, clamping, malformed values, unknown keys, oversized file, failed replacement, reset), parameterised geometry, hover state machine (enter, leave, grace boundary, tab switch, ineligible state, clock going backwards), string-table completeness in both languages, and close-area follow-up of the configured size.
- Windows synthetic tests extended: overlay resize and idempotent placement, settings-window layout at 100/125/150/200% font scaling, language toggle relabelling in place, host round trip with out-of-range values, and a real Ctrl+W chord sent to a throwaway window that must be delivered in full and leave no key down.
- Real-Brave acceptance extended with a second phase: hover mode visibility and hiding, the configured 40 px button, a click on a hover-shown button closing exactly one tab, persistence across an application restart while Brave stays open, and recovery from a corrupted settings file.
- Verification status for this candidate is recorded with its CI run and tag below.

## [0.1.0-dev2] - 2026-10-14

Prerelease. Windows x86/x64, .NET Framework 4.8, C# 5. No dependency added.

### Fixed

- Explicit UI Automation navigation caching: activated cache requests alone do not populate TreeWalker results. The previous cached-property read could throw and hide the ×.
- Incomplete depth/time scans, unknown selection beside a known selected tab, and tabs too short to contain the overlay now reject safely.
- Late close results expire independently of timer ticks; overlay results recheck foreground, window geometry/DPI and age. Pending close validation cannot re-show the button. Transient identity failures do not remain cached.
- Native placement/visibility failures are observable. Error diagnostics include operation, type, HRESULT and sanitized application frames without provider messages or browsing data.
- Dashboard explanations and action rows grow within a scrollable viewport; diagnostics explanations scroll and can be selected/copied. Changed errors, counts and timings refresh correctly.
- Background wait handles and tray resources are disposed. Failure to obtain single-instance protection refuses startup rather than risking duplicate closures. Build-tool discovery supports 32-bit Windows.

### Regression coverage and verification

- Added pure-core deadline, privacy, ambiguity, geometry and close-guard boundary tests.
- Added Windows synthetic UIA cache/scanner, overlay, diagnostic refresh and scaled-font/layout tests, in x86 and x64. These are NOT real-Brave acceptance.
- CI [38575630606](https://github.com/coderunknow/Xtension/actions/runs/38575630606) passed source checks, 38 core tests, six Windows regressions in each architecture, both builds, MSBuild and packaging/provenance/checksum checks. Final release-commit CI is linked from PR #2.
- **Real Brave smoke passed:** actual x86 and x64 EXEs with official Brave v1.97.56 on Windows OS 10.0.26100.0 at 96 DPI, isolated temporary profiles. Startup with Brave already running, overlay position, three tab switches, two windows, focus away/back, Shift/Alt/Win rejection, mouse close removing only the selected runtime identity, minimize/restore/move/resize/maximize, twelve tabs and a long synthetic title.
- Synthetic UIA reproduces the old `Cached.ControlType` InvalidOperationException / HRESULT `0x80131509`; explicit caching succeeds. This supports the missing-button correction, but the user's exact exception/environment was not captured.
- The layout tests exposed and then verified the correction for a clipped startup control at 200% scaled text in a narrow window. Tests exercise 100/125/150/200% font sizes; these are not physical-monitor DPI certification or a visual diacritics review.
- Not fully accepted: physical mixed-monitor DPI, native 32-bit Windows, rapid physical double-clicks/tab switches during validation, intentionally unsupported tab layouts, real hung-provider behavior, tray/startup registration and graceful shutdown. Core/worker safety tests cover deterministic rejection and recovery, not those complete runtime scenarios. Existing post-validation input race remains documented.
- Optional external code reviews were unavailable (Kilo credits, Qodo trial, Copilot quota; CodeRabbit skipped). No review failure was bypassed or described as a successful review; repository CI passes independently. See `docs/DEV2_AUDIT.md`.
- Packaging now refuses stale/dirty/wrong-version/wrong-architecture binaries using commit/hash provenance and checksums. Release workflow additionally downloads published assets and verifies them.

## [0.1.0-dev1] - 2026-10-14

Prerelease. Windows x86 and x64 only.

### Added

- Custom × on the selected horizontal Brave tab, placed at the tab's right edge and moved when the tab changes.
- Dashboard with status and the reason the × is hidden, a turn-on/turn-off control, and a startup option.
- Diagnostics window listing the tabs from the last scan, with scan time and node count.
- Single-instance protection. Launching again opens the running instance's dashboard.
- Startup with Windows, off by default. Per-user registry entry; no administrator rights.
- Unit tests for the pure core (geometry, tab selection, close guard, clock, settings).
- CI workflow (static checks, unit tests, x86/x64 builds, MSBuild project check) and tag-triggered prerelease workflow.
- Release assets: x86 and x64 EXEs, source ZIP from git archive, SHA-256 manifest, and release notes.

### Changed

- The version is generated from `VERSION` into the C# constants, assembly attributes, and the application manifest.
- Overlay geometry is scaled per monitor DPI. Tab recognition thresholds scale with DPI, so a 200% display is handled.
- Scans run on a dedicated thread at 500 ms intervals with a 1000 ms budget and a 1500-node cap. The UI thread no longer waits on UI Automation.
- Dashboard and diagnostics update only when their content changes, and only while visible.
- Disabling the × and exiting are separate actions. Both warn that native close buttons may stay hidden if Brave's own setting is on.
- Logs contain no tab or window titles.
- `Xtension-v0.1.0-source.zip` is no longer stored in the repository. Source archives are built per release.

### Fixed

- A close click is validated again at click time: foreground window, scan freshness, and the × position. A stale or uncertain state sends nothing.
- A late validation result is discarded after a state change or timeout.
- A stalled UI Automation call hides the × after 5 seconds instead of leaving it clickable.
- The × window no longer takes focus from Brave when shown.
- Brave process identity is cached briefly instead of being queried on every check.
- Keyboard input is refused while Shift, Alt, or Win is held, so Ctrl+W cannot become Ctrl+Shift+W.
- Settings are written through a temporary file and then replaced.

### Security and privacy

- No mouse injection. The close path uses only Ctrl+W through SendInput.
- Per-user startup only; the app requests `asInvoker` and no `uiAccess`.

### Verification

- Static checks, unit tests, x86/x64 builds, and the MSBuild project check run in CI for this tag.
- Not verified: behaviour inside Brave (× placement, click-to-close, DPI, Brave setting interaction, tray and dashboard behaviour). These need manual testing on Windows.
- No performance percentage is claimed. Diagnostics reports scan time and node count so these can be measured on a device.
