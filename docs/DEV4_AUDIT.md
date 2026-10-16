# Xtension dev4 audit

Whole-repository audit of the `v0.1.0-dev4` candidate: the real-time behaviour of the × button, stability of the scan
and close paths, the browser-provider boundary, performance profiles, diagnostics, appearance, settings backup and the
application icon. `docs/DEV3_AUDIT.md` is kept unchanged and stays the record of the dev3 findings; this document only
adds to it.

## 1. Scope and method

Inspected: every file under `src/`, `tests/`, `scripts/` and `tools/`, both workflow files, `Xtension.csproj`,
`app.manifest.template`, `.gitattributes`, `VERSION`, and every document (`README.md`, `CHANGELOG.md`, `DESIGN_LOG.md`,
`AGENTS_LOG.md`, `docs/*.md`).

| Method | What it proves | What it cannot prove |
| --- | --- | --- |
| Whole-repository source read | data flow, ownership, ordering, the paths that can carry private data | runtime behaviour |
| `python3 tools/api-check.py .` | every cross-type call matches a declared member with the right arity; every `StringId` has exactly one row | compilation, runtime behaviour |
| `python3 tools/verify-source.py .` | file registration, banned C# 6+ syntax, Vietnamese text confined to the string table, privacy patterns, icon integrity, platform-claim rule | compilation, runtime behaviour |
| tree-sitter C# parse of every changed file | the file parses as C# | semantics, compilation |
| GitHub Actions: `static-and-tests`, `build` (x86+x64), `msbuild-project`, `package-check`, `performance`, `brave-smoke` | compilation, unit tests, the Windows harness, packaging, a real Brave run | the owner's desktop |
| CI performance job (published `v0.1.0-dev3` binary against this candidate, same runner) | direction and magnitude of the change in startup, idle CPU, memory, handles and scan latency | absolute values on other hardware |

**Stated once, and it applies to every finding below: this audit was performed on Linux, where no C# toolchain
(`dotnet`, `mono`, `csc`, `msbuild`, `pwsh`) exists.** Nothing in this repository was compiled or executed locally.
Source inspection can find a defect, but it cannot show that a fix works; every fix below is therefore reported as
fixed-in-source with the run that has to confirm it named explicitly, and any claim that still needs a real run is
labelled `NOT VERIFIED`.

Severity is about consequence, not about how hard the defect is to reach:

- **High** — can hide the × when it should be visible, can show it when it should not, or can affect which tab a close
  would be authorised against.
- **Medium** — can mislead the operator (wrong state, wrong reason, wrong counters), can lose a setting, or can cost a
  whole extra interval of responsiveness.
- **Low** — cosmetic, packaging, documentation, or bounded resource behaviour.

## 2. Findings and remediation

| # | Surface | Severity | Evidence | Root cause | Remediation | Regression coverage | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Foreground browser identity | High | `src/Interop/ProcessIdentity.cs`; dev3 audited the same code as "PID-aware negative identity caching" | the verdict was cached per process id and the executable was matched as one hard-coded name, so a recycled process id or a differently named image could inherit or lose a verdict it had not earned | `src/Core/BrowserCatalog.cs` owns one exact image name and one explicit tab-access contract per browser; `ProcessIdentity.Descriptor(window, fresh)` resolves the foreground window's process id, image name and descriptor, `fresh` bypasses both caches | `tests/UnitTests/BrowserCatalogTests.cs` (7); harness `SupportedBrowserInForeground` | **Fixed** in source; runtime confirmation in the CI `build` and `brave-smoke` jobs |
| 2 | Scan scheduling in `Tick()` | Medium | `src/App/ActiveTabController.cs` | the interval timestamp advanced even when the request was refused (unsupported browser, disabled browser, busy worker), so the next legitimate scan waited a full extra interval for a scan that never ran | the timestamp and the counters now move only when the worker accepted the request; refusals are counted separately and surface as `refused=` in the statistics line | counters are visible in the diagnostics dashboard and in the statistics line; no unit test (needs a live desktop) — **Partial** | **Fixed** in source |
| 3 | × arrival latency | Medium | `Tick()` above; `src/Uia/TabSelectionWatcher.cs`; owner's report | two separate causes: a pointer arriving in the tab strip just after a poll waited out the whole interval, and a tab switch was only noticed at the next poll | hover pointer entry stamps the event time and the pointer gate runs on every tick, so an arrival is served immediately (floor `EventWatchMinScanGapMs`, 80 ms); a best-effort UIA selection/foreground watcher supplies advice-only events, with polling kept as the fallback | `MeasurePointerEntryLatency` and `ReportScanLatency` in `tests/WindowsTests/BraveAcceptance.cs`; `MEASURE-LATENCY` in `scripts/measure-performance.ps1` | **Fixed** in source; latency numbers pending CI |
| 4 | Scan lanes | Medium | `src/Core/ScanMetrics.cs`, `src/App/ActiveTabController.cs` | overlay scans, close validation and dashboard scans shared one queue and one sampling counter, so a dashboard scan could delay an overlay scan and distort the percentiles that describe the × | one lane enum `ScanLane { Overlay, Close, Dashboard }`; requests carry their lane and every metric is recorded per lane | `tests/UnitTests/MetricsTests.cs` (5) | **Fixed** in source |
| 5 | Diagnostics freshness | Medium | `src/UI/DiagnosticsForm.cs`, `src/Core/DiagnosticsReport.cs` | the dashboard rendered a cached report with no timestamp, so an old result could be read as the current state | the report is rebuilt from the live snapshot on every refresh and states the current state and the last scan result separately, with the precise hidden reason, the scan counters and the timings | `MetricsTests` (privacy and immutability of the summary); report text is asserted by the Windows harness indirectly | **Fixed** in source |
| 6 | Unreadable settings | Medium | `src/Core/SettingsStore.cs` | a read failure (corrupt `settings.ini`) silently produced defaults, with nothing in the log to explain why the settings window looked reset | one throttled `WARN` naming only the exception type | `scripts/verify-source.ps1` privacy patterns; nothing else — **Partial** | **Fixed** in source |
| 7 | Backup import preserved values it did not write | Medium | `src/Core/SettingsBackup.cs` | `PreservedKeys` was computed and reported, but `ApplyToStore` only wrote known keys, so a backup's unknown key was announced as preserved and then dropped | `BackupImportResult.PreservedValues` carries the value; `ApplyToStore` writes unknown keys the backup carries, leaves store-only keys untouched, and reports rejected and clamped keys by name | `tests/UnitTests/BackupTests.cs` (7) | **Fixed** in source |
| 8 | Empty browser selection | Medium | `src/Core/AppSettings.cs`, `src/UI/SettingsForm.cs` | clearing every browser wrote an empty mask, which the loader read as "use the default", so the tray could scan Brave after the operator had cleared every browser | an empty mask keeps the previous selection and logs a `WARN`; the settings window refuses to clear the last browser, and diagnostics can say that no browser is enabled | `tests/UnitTests/BrowserCatalogTests.cs`; harness `SettingsWindowAppliesProfilesAppearanceAndBrowsers` | **Fixed** in source |
| 9 | Tray and application icon | Low | `src/UI/TrayIcon.cs`, `src/UI/AppIcon.cs`, `tools/generate-icon.py` | the tray icon was painted with GDI at run time (one size, blurry when the tray is scaled) and no icon resource was embedded at all, so the EXE and the window had no icon | one reproducible multi-size `assets/xtension.ico` (16/20/24/32/40/48/64/128/256, 32-bit), embedded as `Xtension.AppIcon.ico` through `/resource:` in both build scripts and through `EmbeddedResource` for MSBuild; painted icon kept as the fallback and reported by `TrayIcon.UsesEmbedded()` | icon integrity rules in `scripts/verify-source.ps1` and `tools/verify-source.py`; `IconResourceCarriesEverySizeTheTrayAndExplorerAskFor`; the shipped EXE's startup line now reports `tray source=` and `Icon: frames=` | **Fixed** in source; visual review **NOT VERIFIED** |
| 10 | Notification surface | Medium | `src/App/TrayAppContext.cs`, `src/UI/NoticeWindow.cs` | notifications went through a Shell balloon: the shell owns it, it activates, and it can play the system notification sound — all three are consistent with the reported audio crackle, and none of them can be proven from here | one own non-activating (`WS_EX_NOACTIVATE`, `WS_EX_TOOLWINDOW`), sound-free, modeless window, shown and dismissed by the app itself | harness `NoticeWindowNeverActivatesOrTakesTheForeground` asserts the styles and that the foreground window is unchanged across show and dismiss | **Changed**; audio effect **NOT VERIFIED** (see §5) |
| 11 | Settings window did not reflect state | Medium | `src/UI/SettingsForm.cs` | applying a profile left the interval box at its old value, and editing a profile-controlled value kept showing the preset name | applying a profile writes the preset's interval into the box; editing any profile-controlled value reports Custom immediately | harness `SettingsWindowAppliesProfilesAppearanceAndBrowsers` | **Fixed** in source |
| 12 | A profile could silently reset a user value | Medium | `src/Core/PerformanceProfile.cs`, `src/Core/AppSettings.cs` | if the profile marker were authoritative, a user's edited interval would be overwritten on the next load | the marker is a label: the authoritative values are the ordinary documented keys, `Profiles.Resolve` reports Custom as soon as they stop matching a preset, and `ApplyTo` writes only on an explicit selection | `tests/UnitTests/ProfileTests.cs` (6) | **Fixed** in source |
| 13 | Statistics cadence | Low | `src/Core/ScanMetrics.cs`, `src/App/ActiveTabController.cs` | one summary line per 60 s meant that a 20 s measurement window could contain no numbers at all, and the line was written even when nothing had been scanned | one line at most every 15 s or every 50 completed scans, whichever comes first; no line while no scan completed; the line now also carries `samples=` | `tests/UnitTests/MetricsTests.cs`; the `MEASURE-LATENCY` reader in the harness fails to `n/a` rather than to zero | **Fixed** in source |
| 14 | Platform claims | Medium | both gates, all documents | nothing stopped a document from advertising macOS or Linux support while no implementation exists | every line in `README.md`, `CHANGELOG.md`, `docs/RELEASE_CAPABILITIES.md`, `docs/DEV3_AUDIT.md` and this file that names macOS or Linux must also state that the platform is not supported; otherwise both gates fail | the rule itself, checked in this candidate | **Fixed** in source |
| 15 | Worker shutdown | Low | `src/App/ScanWorker.cs` (`_thread.Join(300)`) | a UI Automation call that is still running when the worker is disposed is abandoned after the 300 ms join | accepted, not changed: this is a shutdown path, the instance is never reused, no result from an abandoned scan can be observed and no safety rule depends on it | none | **Accepted** |
| 16 | Repository hygiene | Low | `.gitattributes` | `* text=auto` applied to the new `.ico`/`.png` assets, which are detected as binary today but would have been normalised if they ever parsed as text | explicit `*.ico binary` and `*.png binary` rules | n/a | **Fixed** |
| 17 | Audio crackling | — | see §5 | not reproduced in this environment | candidate change only | none | **NOT VERIFIED** |
| 18 | Multi-platform support | — | see §6 | no implementation exists for either non-Windows platform | documented as blocked, and enforced by the gate in finding 14 | the gate rule | **BLOCKED** (deliberate) |

The dev3 finding 8 (`CacheRequest` leak) is not revisited: dev3 refuted it with the API surface of .NET Framework 4.8
and no dev4 change touches that path.

### Found during verification (added after the first CI runs)

These are the findings the repository's own gates produced once the candidate was actually compiled and executed, and
they are recorded here with the same severity scheme because they are exactly the class of defect this audit is for:
things that source inspection did not reveal and that only a compile or a run could expose.

| # | Surface | Severity | Evidence | Root cause | Remediation | Regression coverage | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 19 | Shared icon handles | High | four settings-window failures in CI: `ObjectDisposedException: Cannot access a disposed object.` | `AppIcon` cached a live `Icon` and returned the same instance to every caller, so the icon test's own `using` disposed it and every window painted afterwards used a destroyed handle | the cache holds the resource bytes only; every call decodes an icon the caller owns, and no instance is ever shared | `IconResourceCarriesEverySizeTheTrayAndExplorerAskFor` disposes one icon and then proves a later icon, a window icon and the tray icon can still be drawn | **Fixed** in source; verified by the harness run |
| 20 | Painted fallback owned no handle | Medium | code inspection after finding 19, then the same class of failure in `TrayIcon.CreatePainted` | the fallback built an icon from `Bitmap.GetHicon` and destroyed that handle in a `finally`, so the icon it returned was already dead while still reporting its size — a test that only reads `Width` cannot see it | the fallback encodes a small 32-bit ICO in memory and decodes it, so the returned icon owns its handle; the layout is the same one the shipped asset uses and was validated against an independent ICO reader before being written in C# | the harness converts the fallback to a bitmap and requires at least one visible pixel, at two sizes | **Fixed** in source; verified by the harness run |
| 21 | Synthetic scanner test bypassed the provider gate | Medium | `FAIL WindowsEntry.ExplicitCacheNavigationAndScannerOnRealWindowsUia: expected 'Ready', actual 'ContractUnimplemented'` | the dev3 test called the handle overload, which resolves the window's identity, and a synthetic fixture window is not an enabled browser — the refusal was correct product behaviour and the call was wrong | the test asserts the refusal (`ContractUnimplemented`, no tabs, no selected tab) and then exercises the bounded walk through the contract overload with the one contract this build implements | the test itself; the identity gate is covered by `BrowserCatalogTests` and by the isolated Brave run | **Fixed** in source; verified by the harness run |
| 22 | The settings window never offered the browser list | High | `FAIL WindowsEntry.SettingsWindowAppliesProfilesAppearanceAndBrowsers: the browser list is offered in the window` in run `38918735937` | `BuildBrowsersGroup` was defined but never called from the constructor, so the group, its checkboxes and the mask they write existed nowhere while the help text beside them described them | the group is built between the performance and advanced sections, in catalog order, with the support tag for each browser | the harness finds each offered browser by its checkbox text and applies the mask; the unit suite covers the catalog and the empty-mask refusal | **Fixed** in source; verified green in run `38921338899` |
| 23 | Two unrelated radio groups unchecked each other | High | `FAIL WindowsEntry.SettingsWindowReflectsHostAndClampsEveryValue: mode shown in the window (expected 'HoverOnSelectedTab', actual 'AlwaysWhenSafe')` in run `38918735937` | the visibility-mode, colour and profile options were all added to one container, and Windows Forms keeps radio buttons mutually exclusive per parent container, so checking a colour (or a profile) unchecked the chosen mode; loading a configuration in hover mode left the window - and the applied settings - on `always` | every logical radio group now has its own `FlowLayoutPanel` row; the harness chooses the mode, then the colour, and requires the mode to be unchanged in the window and in the applied settings | the harness test above; also present in the published dev3 window, where the same layout is in the constructor | **Fixed** in source; verified green in run `38921338899` |
| 24 | Option rows were clipped instead of wrapped, and options were wider than their column | Medium | `FAIL WindowsEntry.SettingsWindowLayoutsAndTogglesLanguage: settings control clipped at 1.5: RadioButton {X=3,Y=38,Width=348,Height=29} in {X=0,Y=0,Width=323,Height=70}` in run `38920031386` | a row laid out horizontally without wrapping cannot move an option to a second line, and a label wider than the column is cut off wherever it is placed: the mode pair, the profile names and the browser support tags were all long enough to be clipped at 150% text in the smallest supported window | the option rows wrap, and the labels are short names with their explanations moved into the help text that already wraps; the assertion now reports the clipped control's text and parent type | `SettingsWindowLayoutsAndTogglesLanguage` walks every control at 100/125/150/200% text in three window sizes, so a clipped option fails the job | **Fixed** in source; verified green in run `38921338899` |
| 25 | The Brave acceptance assertion expected a token the product never writes | Medium | `FAIL Brave acceptance: the shipped EXE starts with the verified browser enabled: … browsers brave.` in run `38920881496` | the assertion looked for `browsers=brave` while the startup line writes `browsers brave`; it had never run before the window harness went green, so the wrong assertion had never been exercised | the expected text comes from `BrowserCatalog.MaskTokenList`, the formatter the product itself uses, and the default mask is asserted explicitly | the isolated smoke job on both architectures now reaches that assertion and passes | **Fixed** in source; verified green in run `38921338899` |

**The verification round passed.** Run `38921338899` on commit `bc999a8` is the first run in which every job the
repository defines is green: static checks and unit tests (`95 passed, 0 failed`), MSBuild project check, Build x64 and
Build x86 (each running the 14-test Windows harness, `14 passed, 0 failed`), package provenance and checksums, the
baseline-vs-candidate performance job, and the isolated real-Brave smoke on both architectures. The third-party
`Kilo Code Review` app reports `action_required` with "Review could not start because the account has insufficient
credits", which is an account condition of that app and not a result of this code; it is not a job of this
repository's workflow. The real-browser numbers are in §3; the acceptance run reports Brave `155.1.97.56`,
Windows `10.0.26100.0`, DPI 96, and 19 staged checks passing on both architectures.

Two further corrections belong to the same CI round. `Profiles.Clone` was called on three paths and had never been
written, so the candidate did not compile (`CS0103`); `AppIcon` used `SystemInformation` without importing
`System.Windows.Forms` (`CS0103`); and the static check's own new path rules used doubled separators and .NET regex
escaping, which ended the script with no readable reason — it now reports its failures as a check annotation
(finding list §1 method table). None of those were design defects: they were the ordinary cost of writing C# with no
compiler available, which is why the gates and not the author are the verification, and why every fix above names the
run that has to confirm it.

## 3. The × button: where the time actually went

The owner's report was that the × appears late or not at all. Latency was traced through the source, not guessed at
from the interval, and three separate costs were identified. Only the first two are code costs; the third is the point
that the trace had to establish — how long UI Automation itself takes on the target machine.

| Segment | What it costs | Status after this candidate |
| --- | --- | --- |
| Pointer enters the tab strip → a scan is submitted | up to one full `scanIntervalMs` (default 500 ms) in hover mode, because the request was interval-gated and a tab switch was only seen at a poll | served on the tick that observes the arrival; a tab switch is served from a UIA event, with polling as the fallback; floor 80 ms between event-driven requests |
| Scan submitted → a result is frozen | the scan budget (1000 ms / 1500 nodes) plus queueing; the queue was shared with dashboard scans | a UI Automation scan of a real Brave window, measured with the product's own scanner, reported as `uiaScanMsP50/P90` by the harness and as `scanMs p50/p95/max` in the statistics line |
| Result frozen → the overlay is visible | a placement on the UI thread, previously redone on every tick | the overlay placement is idempotent; the same rectangle is used for painting, placement, hit testing and click validation |

How this is measured, and what is honest about it:

- `tests/WindowsTests/Measurement.cs` measures a *shipped* EXE: time from process start to its own readiness line, idle
  CPU, working set, private bytes, handles, how many state lines it wrote while being sampled, and the scan latency of
  the same scanner against the same fixture window. `scripts/measure-performance.ps1` runs that against the published
  `v0.1.0-dev3` and against this candidate on one runner, and now also publishes the candidate's own counters and
  timings (`MEASURE-LATENCY`), parsed from the lines that run appended to the product log: counts, refused, stale,
  discarded, errors, stalls, `scanMs p50/p95/max`, `queueMs p95`, events, samples and event-to-visible.
- `tests/WindowsTests/BraveAcceptance.cs` adds the two numbers the owner's report is actually about: pointer entry →
  visible (`MeasurePointerEntryLatency`, ceiling 3000 ms so a regression fails the job instead of being ignored) and
  ten real-browser scans for percentiles.
- **Measured in CI now** (run `38921338899`; the numbers themselves are reproduced in the changelog and the final
  report): the pointer-entry path is `156` ms on x64 and `150` ms on x86 for a re-entry onto the selected tab, and
  `150`/`157` ms for the first show from outside the tab strip, which includes the harness moving the pointer and the
  confirmation scan that follows it - inside the 3000 ms ceiling the acceptance run enforces. Ten harness-side
  UI Automation scans of the real browser took p50 `58`/`62` ms, p95 `70`/`77` ms, max `73`/`78` ms (x64/x86). The
  product's own statistics line reports `scanMsP50=12.0 scanMsP95=17.0 scanMsMax=54` over 30 completed scans with
  `refused=0 stale=0 errors=0 stalls=0 queueMsP95=0.0`, against `uiaScanMsP50=33.7` for the published dev3 binary in
  the same job. The settings window was built and shown in `933`/`1118` ms (x64/x86 smoke) and `1043`/`1428` ms (x64/x86
  build jobs).
- **NOT VERIFIED locally.** There is no Windows desktop in this environment; every number above comes from the CI
  runs recorded in the pull request, and the harness in that environment is the only place these paths execute. No
  number in this document is estimated, and no percentage improvement is claimed anywhere.

## 4. Safety invariants (unchanged, deliberately)

Nothing in this candidate relaxes a validation rule, and no metric is used as an acceptance threshold. Unchanged and
re-checked in the source: the fresh-scan requirement before any injection, the exact selected-tab match, the click
inside the confirmed × rectangle, the epoch rule, geometry/DPI and foreground identity agreement, the Shift/Alt/Win
refusal, the close guard authorising immediately before the injected input, the close cooldown (800 ms), the staleness
window (1500 ms), the stall threshold (5000 ms), the close-validation deadline (3000 ms), the scan budget (1000 ms /
1500 nodes / depth 12 / 400 siblings), and the "hide or disable rather than guess" rule for incomplete, stale, ambiguous
or timed-out scans. The performance profiles choose a cadence and a poll interval only; they cannot select a budget, a
deadline, a cooldown or a confirmation rule, and the profile values are inside the documented setting ranges.

## 5. Audio crackling

The report: brief distortion while Xtension shows a notification. This environment has no audio device, no Windows
desktop and no way to run the binary, so **nothing was reproduced and no cause is claimed as proven.** What was done:

- Traced the notification path end to end: tray tip → Shell balloon (shell-owned window, activation, possible system
  notification sound) → overlay/UI-thread work on the same thread that places the ×.
- The candidate change removes the shell from the path: one own window that never activates
  (`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), plays no sound, does not steal focus, is sized in logical pixels with
  per-monitor DPI handling and has a bounded auto-close. The tray balloon is no longer used.
- The harness now asserts the non-activation contract, so the property cannot quietly regress.
- **NOT VERIFIED:** the crackle itself. It has not been reproduced, so the fix is not claimed to be the cause. Closing
  this needs a run on a machine with real audio playback: play audio, trigger the notice repeatedly, capture the
  system audio output through a loopback device and compare with the notice disabled; only that comparison can show
  whether the change removed the artefact. No system-wide audio setting was changed, and notifications were not
  disabled.

## 6. Multi-platform: exact blockers

Windows is the only supported platform, and the gate in finding 14 fails if any document claims otherwise.
macOS is not supported and Linux is not supported; neither has an implementation here, and no interface-only or
placeholder work was done for them, because a placeholder is not support. The blockers are concrete:

| Platform | Blocker | Evidence |
| --- | --- | --- |
| macOS | The window and input layer has no counterpart in this codebase: `WS_EX_NOACTIVATE` click-through placement, `SetWinEventHook` foreground tracking and `GetForegroundWindow` have no direct equivalent; the app would need the Accessibility (TCC) permission before any UI Automation call, and denied permission has to be a first-class state, not an error string | `src/Interop/NativeMethods.cs`, `src/Interop/ForegroundWatcher.cs`, `src/Interop/KeyboardInjector.cs` |
| macOS | The selected-tab and tab-strip providers are Windows UI Automation; macOS has no implementation, and one would need the Accessibility tree of each browser plus its own provider contract, which is a different scanner rather than a port | `src/Uia/UiaTabScanner.cs`, `src/Core/BrowserCatalog.cs` (`TabAccessContract`) |
| Linux | UI Automation does not exist; the equivalent is AT-SPI over D-Bus, with a different tree model, and Wayland offers no way for an ordinary client to read another window's geometry or to move a click-through overlay, so X11 and Wayland would be two separate implementations | same two files as above |
| Both | The supported runtime contract is .NET Framework 4.8 and C# 5 on Windows; the managed `UIAutomationClient` assemblies are Windows-only, and the build, packaging, checksum and release scripts are Windows/PowerShell | `Xtension.csproj`, `src/Uia/UiaTabScanner.cs`, `scripts/*.ps1`, `.github/workflows/*.yml` |

Consequence for this candidate: no platform support is advertised, `docs/RELEASE_CAPABILITIES.md` and `README.md` state
the Windows-only scope, and the readiness of any future port is defined as "measured against a real browser on that
platform", which no part of this repository can do today.

## 7. Still not verified

| Surface | Status | Why it is unverified / what would verify it |
| --- | --- | --- |
| Every dev4 change compiles and passes the harness | **NOT RUN locally** | No C# toolchain in this environment. The CI `static-and-tests`, `build`, `msbuild-project` and `package-check` jobs are the first real check. |
| dev3 baseline versus dev4 candidate numbers | **Pending CI** | Needs the `performance` job, which runs both binaries on one runner. |
| Pointer-entry → visible latency, scan percentiles | **Pending CI** | Needs the `brave-smoke` job with a disposable profile; the harness prints the numbers and fails on the latency ceiling. |
| Chrome, Edge, Firefox acceptance | **NOT RUN** | Declared unsupported: the scanner is gated on the tab-access contract, and no acceptance evidence exists for them. Discovery is implemented; detection is not. |
| Real icon appearance in the tray at 100%/125%/150%/200% | **NOT VERIFIED** | Needs a human eye on a real desktop. What *is* verified: the shipped file has nine 32-bit frames at the documented sizes, every frame lies inside the file, and an independent ICO reader decodes all of them with 252/256, 564/576, 960/1024 and 2040/2304 visible pixels at 16, 24, 32 and 48 pixels — i.e. the small frames are not blank. How they look is still a human judgement. |
| Audio crackle cause and the effect of the notice-window change | **NOT VERIFIED** | Needs a machine with real audio playback and a loopback capture; see §5. |
| Physical multi-monitor, mixed DPI, native 32-bit Windows | **NOT RUN** | CI runners are single-display at 96 DPI on an x64 host. |
| Settings window at 200% text scaling and at its minimum width, in both languages | **NOT VERIFIED** | Layout is asserted for the model, not visually reviewed. |
| A genuinely hung UI Automation provider | **NOT RUN** | The stall path, deadline expiry and fail-closed behaviour are unit and synthetic tested; a truly hung provider was not reproduced. |

## 8. What this candidate does not claim

- It does not claim to be free of defects, and it does not claim that every dev3 gap is closed.
- It does not claim that source inspection is a substitute for the runs listed in §7 as `NOT RUN` / `NOT VERIFIED`.
- It does not claim that the performance comparison is a benchmark: it is one sample per binary on one runner, and it
  is reported as direction and magnitude with the raw values.
- It does not claim that Chrome, Edge or Firefox work, or that any of the discovery code for them is support.
- macOS and Linux are not supported: neither platform has any implementation in this candidate, and neither one is
  advertised as a capability of this release.
- It does not claim that the audio crackle is fixed: the popup path was changed and the change is unverified against
  real audio playback.
- It does not claim third-party review; no review service was used for this candidate.
- It does not claim that hover is a security boundary. It is a display rule; the click path is the boundary.
