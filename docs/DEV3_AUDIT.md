# dev3 audit — findings, fixes and remaining risk

Candidate: `0.1.0-dev3`, branched from `main` at `5917dede9589b8f1922734d2c03a618c3a541efc` (the merge commit of
PR #2, which is the dev2 baseline). This document records what was audited, what was changed, what was proven by
which kind of evidence, and what is still not verified. It is written so that a reviewer can disagree with a
conclusion by looking at the evidence instead of taking a claim on trust.

Severity scale: **high** = could close the wrong tab, lose data or freeze the app; **medium** = wrong state,
wasted work or a visible defect; **low** = cosmetic, wording or maintenance risk.

## 1. Method and its limits

| Evidence | Where it runs | What it can and cannot show |
| --- | --- | --- |
| `scripts/verify-source.ps1` | Windows CI and (mirrored in `tools/verify-source.py`) the audit environment | File/version/csproj/manifest/workflow consistency, C# 5 language rules, no mouse injection, no private text in logs, ASCII scripts, no secrets. It cannot prove that code compiles. |
| `scripts/test.ps1` (pure core) | Windows CI | Compiles `src/Core` with `csc /langversion:5` and runs the reflection test runner. Real compiler, real assertions. |
| `scripts/test-windows.ps1` | Windows CI, x86 and x64 | Compiles the whole application plus the harness and runs synthetic UI Automation, overlay, layout, settings-window and injection tests on real Windows. Not a browser. |
| `scripts/test-brave.ps1` | Windows CI, x86 and x64, disposable runner | Runs the shipped EXE against an isolated official Brave build. Real browser, real clicks, real UI Automation. |
| Performance job | Windows CI | Runs the published previous release EXE and the candidate EXE on the same runner and prints one machine-readable line per run. A comparison, not a pass/fail threshold. |
| Owner manual pass | Owner's own Windows desktop | The only evidence for physical monitors, real DPI scaling, the visual result in Vietnamese/English and daily-use responsiveness. Required before the tag. |

The audit environment that produced this change has no C# compiler, no PowerShell and no Windows desktop. Nothing in
this repository was built, tested or run there. Local work was limited to reading the sources, a Python mirror of the
static rules, an identifier/table cross-check (`tools/api-check.py`) and a tree-sitter C# parse. Every compile, unit
test, synthetic test and Brave run reported for this candidate is a Windows CI result and is quoted with its run id.

## 2. Findings

| # | Severity | Area | Finding | Status |
| --- | --- | --- | --- | --- |
| 1 | medium | `SettingsStore` | The file was read and parsed again on every getter call; the settings path is consulted by the tray, the dashboard and (now) the settings window, so parsing repeated per interaction rather than once. | **Fixed.** Values are cached in memory behind a gate, with an explicit `Reload()` for recovery paths. |
| 2 | medium | `SettingsStore` | `File.ReadAllLines` loaded an unbounded file into memory, so any large or wrong file at that path (for example a dropped file or a redirected path) could exhaust memory at startup. | **Fixed.** Files larger than `AppInfo.MaxSettingsFileBytes` (64 KiB) are ignored, logged once and left untouched on disk; defaults are used. |
| 3 | medium | `SettingsStore` | Rewrites only wrote keys the app knew about, so any newer or third-party key in the file was silently destroyed. | **Fixed.** Unknown keys are preserved and rewritten; covered by a test. |
| 4 | medium | `SettingsStore` | A failed `File.Replace` left `settings.ini.tmp` behind forever, and a later start could read stale partial data if it picked the temporary file as the settings file. | **Fixed.** The temporary file is deleted on any failure and `settings.ini` is never read from a partial write; covered by a test using a blocked temporary path. |
| 5 | medium | `SettingsStore` / typed settings | There was no typed settings API, no clamping and no validation, so any future call site would have had to parse values itself and could trust an out-of-range number. | **Fixed.** `AppSettings` holds the typed values with documented ranges, `Normalize()` repairs anything out of range, and `AppSettings.Defaults()` is the reset target. |
| 6 | medium | Overlay placement | Every scan called `SetWindowPos`, re-read the window bounds and invalidated the window even when nothing had moved, which is pure cost at 2 scans per second. | **Fixed.** Placement is idempotent: an unchanged, already verified placement returns immediately; the window is still verified after every move, and a failed verification hides the button. |
| 7 | medium | Process identity | Only positive verdicts were cached. With a non-Brave foreground (the common case outside the browser) a process handle was opened, queried and closed on every 250 ms tick. | **Fixed.** Negative verdicts are cached per window and process id with a shorter lifetime so a transient failure heals quickly. The path that sends Ctrl+W still passes `fresh: true` and re-queries. |
| 8 | medium | UI Automation | Each scan created a `CacheRequest`, an unmanaged UI Automation object, and never disposed it. At two scans per second that accumulates objects for the whole session. | **Fixed.** The request is disposed in a `finally` after every scan, including error and timeout paths. |
| 9 | low | Controller | Every timer tick built a snapshot and pushed a render, even when nothing observable had changed, so the dashboard and diagnostics re-compared unchanged data four times per second. | **Fixed.** The controller compares the observable fields and notifies only on a real change; `ForceNotify()` exists for changes that must repaint (language, settings). |
| 10 | low | Controller wording | A minimized non-Brave window could be described as "Brave minimized", because the message condition tested `minimized` without testing the process identity. | **Fixed.** The wording now requires a confirmed Brave identity. |
| 11 | low | Tooling | The csproj, `src/` tree and workflow required-file list are three places that must agree, and the static script only checked the first two. | **Adjusted.** The required-file list now includes this document and the new scripts, and the localisation rule below was added. |
| 12 | low | Documentation | The previous audit's acceptance matrix did not distinguish "not attempted" from "attempted and failed", which makes the remaining gaps hard to read. | **Adjusted.** Section 5 lists each unverified surface with the exact reason it is unverified. |

No finding in this list weakens a safety rule. Findings 1–5 are storage and consistency, 6–8 are cost with no
behavioural slack, 9–10 are reporting, 11–12 are process.

## 3. New behaviour and its contract

**Hover mode.** Eligibility is derived only from a fresh `Ready` scan of the current foreground Brave window: the
region is the confirmed selected tab's rectangle unioned with the button's own rectangle. The pointer position decides
whether the button is *displayed*, never whether it may *close*: a click still goes through the unchanged
`CloseGuard` path with a new scan. Leaving the region hides the button after a bounded keep-time (default 250 ms,
0–1000 ms) so the pointer can travel from the tab onto the button. A tab switch, a foreground change, a minimize, a
geometry change, a mode change, a stalled scan or any failed confirmation resets the hover state immediately, without
the keep-time, so a hover that belonged to a previous tab cannot keep a button alive.

**Scan gating in hover mode.** While the overlay is hidden and the pointer is outside the window's tab strip, no
UI Automation scan is submitted. A pointer outside the strip cannot satisfy any eligibility rule, so this can only
delay showing the button by one interval; it cannot authorise anything. An unknown pointer position, an empty window
rectangle or an empty strip falls back to scanning.

**Button size and inset.** Bounded logical pixels, clamped at load and at input. `TabGeometry.GetOverlayBounds` is the
single source of the rectangle used for painting, window placement, hit testing and click validation, so a size change
cannot leave the native window or the validated area at the old size. If the configured button and inset cannot fit
inside the tab, the outcome is `SelectedTabTooSmall` and the button stays hidden.

**Language.** One table in `src/Core/Strings.cs` holds every user-visible string in Vietnamese and English, addressed
by `StringId`. Scan results store a message key, not text, so the explanation of the last scan re-renders in the new
language without waiting for another scan. Log lines and internal decision reasons stay English, deliberately, because
they are diagnostic. `scripts/verify-source.ps1` now fails if Vietnamese text appears outside the string table, which
is what keeps this property from drifting.

**Settings file.** `%LOCALAPPDATA%\Xtension\settings.ini`, keys `overlayEnabled`, `visibilityMode`, `buttonSize`,
`buttonInset`, `hoverGraceMs`, `scanIntervalMs`, `language`. Start-with-Windows remains a per-user registry `Run`
entry and is not duplicated into the file, so there is exactly one source of truth for it. Every change from the
settings window, the tray menu or the dashboard goes through the same application-level methods, applies to the
running instance and is then persisted; a failed write is reported and the value still applies for the session.

## 4. Performance work

Changed: idempotent overlay placement, PID-aware negative identity caching, change-gated notification, `CacheRequest`
disposal, per-scan stopwatch reuse, and hover-mode scan gating. Not changed: the scan interval default (500 ms), the
scan budget (1000 ms / 1500 nodes), the stall threshold (5000 ms), the staleness window (1500 ms), the close
validation deadline (3000 ms), the cooldown (800 ms) and the number of worker lanes (two). No validation step was
removed or made optional to gain speed, and no metric is used as an acceptance threshold: the job exists to show the
direction and the magnitude, and both binaries are measured back to back on one runner so the comparison is fair even
if the runner is noisy.

The scan interval and the hover keep-time are exposed as bounded settings, so a user on a slow machine can trade
responsiveness for CPU explicitly, with a documented minimum of 300 ms.

## 5. Still not verified

| Surface | Status | Why it is unverified / what would verify it |
| --- | --- | --- |
| Physical monitors with mixed DPI, 125%/150%/200% scaling | **Not attempted** | CI runners are single-display at 96 DPI. Only the owner's desktop can cover it. |
| Native 32-bit Windows (not WoW64 on x64) | **Not attempted** | CI builds and tests x86 binaries on an x64 host. |
| Visual review of the settings window, hover mode and both languages | **Not attempted** | Needs a human eye on a real desktop; scaled-font assertions are not a visual review. |
| Rapid physical clicking / tab switching while a validation is in flight | **Not attempted** | Synthetic tests cover deterministic rejection; physical race stress needs the owner's desktop. |
| Real hung-provider recovery (UI Automation call never returns) | **Partially covered** | The stall path, deadline expiry and fail-closed behaviour are unit/synthetic tested; a truly hung provider was not reproduced. |
| Hover-mode CPU on a low-end machine over a long session | **Partially covered** | The CI job samples idle CPU for a bounded window on a fast runner. A long session on the owner's machine is the real test. |
| Unusual Brave layouts (pinned, vertical, scrolled-out tabs) | **Documented, not accepted** | Such tabs never satisfy the selection/strip rules, so the button hides. Not exercised against real Brave in this candidate. |
| Owner manual acceptance of this exact build | **Pending** | The owner runs the CI-built EXE before the tag. Until that report exists, this candidate is not released. |

## 6. What this candidate does not claim

- It does not claim to be free of bugs, and it does not claim that every dev2 gap is closed.
- It does not claim that the resource comparison is a benchmark: it is one sample per binary on one runner.
- It does not claim that the new settings window was visually reviewed.
- It does not claim any third-party code review. Optional review services were unavailable in earlier cycles for
  credit/quota reasons and were not re-attempted here; repository CI is the verification that was actually observed.
- It does not claim that hovering is a security boundary. It is a display rule; the click path is the boundary.
