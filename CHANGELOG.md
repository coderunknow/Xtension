# Changelog

All notable changes are listed here. Versions follow SemVer. The version in `VERSION` is the single source.

## [0.1.0-dev2] - Unreleased candidate

Windows x86/x64, .NET Framework 4.8, C# 5. Publication is gated on verification.

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
- Candidate CI results pending. No Windows/Brave runtime test was performed locally. Actual user exception, Brave version, mixed-monitor DPI behavior and real click-to-close remain unverified; publication decision follows final checks.

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
