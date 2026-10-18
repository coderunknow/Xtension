# dev5 audit and acceptance record

Date: 2026-10-18. Prerelease audit; recorded verification is below. Publication is separately gated, not implied.
Baseline: main `685975ec088ac28a722e004d6354c9fc1cbd6e63`, dev4 tag `15900f5`.

## Findings (verification run 39108878064; publication remains separately gated)

| ID | Severity | Evidence and root cause | Impact / reproduction | Remediation and regression | Status / residual risk |
| --- | --- | --- | --- | --- | --- |
| D5-01 | High | `TabInfo`, `CloseGuard`, `OnOverlayClicked`: displayed identity was not carried into close validation | select a different tab into identical bounds before validation; old guard accepted it | capture UIA runtime ID from displayed result, compare exact nonempty array against fresh result; unit regression with identical geometry/different ID | Fixed, core + Windows/real-browser regressions passed; post-validation input race remains |
| D5-02 | Medium | `SettingsStore.SetMany` mutated `_values` before validation/write finished | blocked temporary path or later invalid key leaves earlier values in cache; later Save persists failed changes | copy, validate, atomically write then publish cache; regression for failed I/O and invalid batches | Fixed, core/Windows regressions passed; no cross-process settings editor synchronization |
| D5-03 | Medium | controller copied only selected fields on ApplySettings, returned its mutable settings reference | changed colour/opacity/border/language appear in overlay but diagnostics retain old values | copy ownership in/out; apply all fields and invalidate on appearance change; Windows snapshot regression | Fixed, Windows regression passed |
| D5-04 | Medium | scanner checked contract enum only, not descriptor implementation; walked any TabItem without tab-list parent | unsupported Chrome descriptor could enter shared scan overload; unrelated TabItem could look horizontal | descriptor implementation gate and direct Tab parent contract; actual browsers and synthetic fixture must verify it | Fixed, descriptor refusal + real Brave/Chrome passed |
| D5-05 | Medium | `RequestImmediateTick` changed UI-owned flags/64-bit timestamps on UIA callback thread | concurrent event delivery/read, including x86 torn timestamps | coalesced callback writes timing/state on UI thread only | Fixed by source + real switch smoke; exhaustive stress unverified; events remain advisory |
| D5-06 | Low | WinEvent hook range 3–22 includes unrelated system events; handler did not filter | menu/capture events ask for unnecessary scans | only foreground and minimize start/end trigger callback | Fixed by source; no measured CPU claim |
| D5-07 | Medium | latency harness caught failed hover-hide and returned success; scan sampler returned silently on failed scan | acceptance could pass without advertised latency evidence | require every sample and hide prerequisite, fail rather than skip | Fixed, actual browser latency samples required and passed |
| D5-08 | Low | close completions excluded from metric Record although three lanes were documented | close lane silently absent from scan sample population | record every completed lane using same bounded buffer | Fixed, core metrics regression passed; per-lane display subsequently added in D5-14 |
| D5-09 | Informational | dev4 capabilities points at dev3 audit, README says backup JSON while implementation is key=value | misleading operational documentation | update current docs, retain historical logs | Corrected in current docs |

## Browser acceptance gate

Brave and Chrome passed independent intermediate actual-EXE acceptance in both architectures (run `39108277770`, commit `5c97764`). They use exact executable identity, direct UIA Tab → TabItem, unique known SelectionItem selected state, runtime identity, bounded walk and existing horizontal geometry. Merely setting the catalog entry is not acceptance. Default remains Brave-only. Edge is disabled after the reproducible failure below. Each offered browser must pass an isolated-profile packaged-EXE placement/close/refusal/hover/persistence test on both process architectures. Firefox is unsupported: Gecko UIA mapping is not implemented or verified. No placeholder provider.

## Verification at initial implementation (historical)

Local tools/api-check.py and tools/verify-source.py pass (aids, not compilation). Actual scripts, x86/x64 builds, synthetic Windows harness, package provenance, performance and real-browser acceptance are pending Windows CI. Direct local dev4 asset download failed at disallowed release-assets host; no local hash is claimed. CI must verify it before execution. No tag or release created.

## Additional trace findings

| ID | Severity | Evidence / root cause / reproduction | Remediation and regression | Status / residual risk |
| --- | --- | --- | --- | --- |
| D5-10 | High | overlay `_armed` survived real moves, and same-bounds identity replacement was not a mouse gesture boundary; mouse-down on A then target changes before mouse-up could click B | cancel press on placement or displayed-identity change; copy actual displayed identity before hiding; synthetic down/change/up versus normal click | Fixed; synthetic/real smoke passed; input after fresh validation still has unavoidable OS scheduling race |
| D5-11 | Medium | `TabSelectionWatcher.Unregister` cleared root/flag but retained `_registered`; A → zero → A skipped Register forever | reset registered HWND on unsubscribe, handle disposed wake safely; actual synthetic UIA subscribe/unsubscribe/resubscribe regression | Fixed; synthetic regression passed; a genuinely hung registration remains a bounded abandoned background thread, not forcibly aborted |
| D5-12 | Medium | `SettingsForm.ImportBackup` set result text, then LoadFromHost/RefreshStatus overwrote it with visibility mode | refresh first and show actual import result last; test through the same internal non-dialog method the file dialog calls | Fixed; synthetic regression passed; no redesign |
| D5-13 | Medium | release workflow previously allowed any version-matching prerelease tag without successful exact-commit main CI, and rebuilt EXEs without real-browser release-byte acceptance | tag gate requires current merged main SHA and successful push CI for that SHA; six release browser jobs use exact artifacts that publish packages | Implemented, end-to-end pending; no old tags/assets altered |

## Feature ranking and platform decision

1. Opt-in browser expansion: high value for existing workflow, moderate provider maintenance, no new runtime dependency; acceptance is actual isolated-profile EXE placement, exact single-tab close, disabled/modifier refusal and lifecycle on both architectures. Chrome independently passed; Edge failed and was withdrawn, not hidden behind an experimental label. One additional browser delivered, not the preferred two.
2. Complete future-key backups: concrete gap in dev4 export, small bounded core implementation. Export includes unknown store keys except schema metadata; round-trip regression exercises export/parse/apply. Implemented; round-trip core regressions passed. Backups contain configuration, including user-authored unknown values; inspect before sharing.
3. Global shortcuts: deferred, little repository evidence, input/focus conflicts and unnecessary hooks.
4. New platform host: deferred, high scope/risk and no native GUI acceptance environment.

macOS and Linux X11/Wayland remain unsupported. See [platform feasibility](PLATFORM_FEASIBILITY.md) for native accessibility/overlay/input boundaries, alternatives, relative costs and real-browser/permissions/packaging gates. No fake port or placeholder artifact is delivered.

| ID | Severity | Evidence / root cause | Remediation and regression | Status / residual risk |
| --- | --- | --- | --- | --- |
| D5-14 | Low | `ScanMetrics` retained lane tags but exposed aggregate percentiles only; the old lane test checked enum distinctness rather than separation | expose retained sample counts/scan and queue p95 for each lane; regression uses deliberately different lane durations and checks the rendered report | Implemented, core regression passed; counts are bounded-window samples, lifetime request/refusal/error counters remain aggregate |
| D5-15 | Low | rejected-backup text promised current values although Parse deliberately starts from defaults; backup text promised no arbitrary content despite preserved unknown settings | bilingual text now states documented defaults and advises inspecting custom values before sharing; current docs describe INI/clamping rather than JSON/defaulting all numbers | Implemented; bilingual consistency/static tests passed |

## Repository coverage and unchanged controls

- Safety trace: overlay press/release → controller → independent close worker → scanner → TabSelector → CloseGuard →
  final foreground/modifier/deadline read → SendInput. All original budgets, depth/node/sibling caps, geometry, DPI,
  epoch, foreground, deadline, stale-result and cooldown conditions retained. Runtime identity is an additional gate.
- UIA: explicit cached navigation remains correct; CacheRequest activation is disposed, not the nondisposable request.
  Documents/Edit/Text/Image/Button/ScrollBar are leaves, not page-content traversal. Selection ambiguity and unknown
  state refuse. No attempt to abort a native provider call or accumulate replacement threads.
- Concurrency/lifecycle: three bounded scan lanes, one pending/running per worker, MTA UIA threads, bounded shutdown
  joins and background-thread exit; registration remains best effort. UI-thread-only event state is now enforced.
  Process identity is PID-aware and the close path queries it uncached. Single-instance mutex failure remains closed.
- Settings/UI: Normalize/range constants and single host path retained; all values appear in diagnostics snapshots.
  Unknown keys preserved; invalid batches and failed writes leave cache/disk committed. Output format cannot inject
  another line; maximum written file size is enforced. Read errors and oversized files still fall back with warning.
  A racing external settings editor and file growth between stat/read are not fully guarded; local settings are not
  a security boundary against another process running as the user. Import outcome visibility was a verified UI defect;
  no speculative layout or icon redesign was done.
- Resource/privacy: fixed-size timing buffers, bounded rotated log, idempotent overlay placement, throttle warnings,
  disposable native handles/brushes/fonts and no titles/URLs/provider exception messages in persistent diagnostics.
  Per-lane samples are now actually reported. No measured speedup claimed. Readiness marker is tray construction,
  not first-overlay readiness; real hover latency is measured separately.
- Build/release: explicit compile lists, VERSION generation, architecture/version/commit/dirty/hash provenance,
  source git archive, negative packaging cases, MIT unchanged and ASCII scripts retained. Framework csc output is
  provenance-reproducible, not claimed byte-for-byte deterministic (timestamps/toolchain can differ). Existing tags,
  published releases and assets remain unchanged. Optional review apps are not repository CI and have no assumed result.

## Remaining environment gates

Physical multi-monitor/mixed DPI, native x86 Windows (not merely x86 process on x64), pinned/vertical/scrolled layouts,
true hung-provider recovery, real tray shell/startup registry/graceful shutdown integration and human visual/icon review
remain incompletely verified. These are disclosed prerelease limitations, not waived safety checks. The owner no longer
observes the audio symptom; no reproduced root cause or new audio regression is claimed.

### Intermediate performance evidence (not final candidate)

Run [39106587826](https://github.com/coderunknow/Xtension/actions/runs/39106587826), commit `802b203`, performance
job `117613979110` succeeded. Baseline executable independently matched SHA-256
`35b5a67b2c416666b61d43dc1451e8e17c49c081c65226ec5e8cea407c6cce82`, source `15900f524951b14ec7658e2ddadf07166f10ed3a`,
product version `0.1.0-dev4`. These hashes were computed in Windows CI, not locally. Intermediate first repetition,
x64 process on Windows 10.0.26100.0, pinned Brave, equivalent fresh three-blank-tab profiles, default configuration:

| State / binary | Ready ms | CPU % of one core | Working/private MiB | Handles | Sample seconds | Scan p50/p90 ms |
| --- | ---: | ---: | --- | ---: | ---: | --- |
| idle / dev4 | 326 | 0.00 | 33.6 / 25.0 | 325 | 20.3 | n/a |
| idle / candidate | 205 | 0.00 | 33.6 / 25.0 | 325 | 20.1 | n/a |
| foreground / dev4 | 217 | 2.74 | 53.4 / 37.8 | 420 | 20.6 | 73.9 / 85.3 |
| foreground / candidate | 207 | 2.88 | 53.3 / 37.7 | 420 | 20.6 | 80.9 / 91.1 |

Candidate product counters at the last statistics line: 25 completed, zero refused/stale/discarded/errors/stalls,
scan p50/p95/max 78/99/108 ms, queue p95 0 ms; baseline 26 completed, zero failures, 72/86/160 ms and queue p95 0 ms.
Pointer was not moved; events/event-to-visible unmeasured, not zero latency. Three repetitions ran, but GitHub caps
annotations per step and only the first observations were retrievable here; complete raw report is the run's
`performance-evidence` artifact. Add a separate single full-report annotation for subsequent runs. No measured
speedup or statistically justified regression threshold is claimed from this partial intermediate evidence.

### New-browser run outcome

Run `39106587826` did **not** pass overall: Chrome and Edge in both architectures hit the unchanged 15-minute job
limit after signed-fixture verification (Chrome `155.0.8059.40`, Edge `154.0.4258.62`). No real acceptance result was
retrievable from the synchronous native pipeline. Root cause remains unconfirmed; bounded file-backed fixture I/O
and phase markers are the next diagnostic/remediation step, not a waiver. Brave and performance jobs succeeded.

## Intermediate verification and scope resolution

Run [39107697740](https://github.com/coderunknow/Xtension/actions/runs/39107697740), `1a1bfdf`: static and 100 core
checks, MSBuild, both Framework builds, package/provenance, performance, both Brave and both Chrome actual-EXE
acceptance jobs passed. **Both Edge acceptance jobs failed** after hover-close/application restart.
Run [39108277770](https://github.com/coderunknow/Xtension/actions/runs/39108277770), `5c97764` reproduced this and
recorded `DepthBudget`, 197 nodes, 160/181 ms, in both architectures. This is not a time-budget failure or evidence
that increasing the budget is safe. The same run passed **19 synthetic Windows regressions per architecture**,
including repeated TrayAppContext teardown and primary/secondary named-event delivery. These are not physical tray
Shell/startup-registry acceptance. Chrome tested version `155.0.8059.40`; attempted Edge `154.0.4258.62`.

**Resolution:** Edge is unsupported and explicitly scanner-disabled. Exact executable detection alone is retained;
settings, imported tokens and hostile masks cannot enable it. Both CI matrices keep real Edge jobs, explicitly named
**refusal**, requiring no UIA traversal, selected target or overlay from the actual packaged executable even with
forced Edge settings. They are not successful Edge acceptance. A safe stable Edge chrome-root provider remains
follow-up; depth/node/time caps, incomplete refusal and every close guard remain unchanged. Chrome carries real
acceptance evidence, not an engine-based assumption. Firefox remains unsupported without a placeholder provider.

Additional findings:

| ID | Severity | Evidence / root cause | Reproduction / remediation | Status |
| --- | --- | --- | --- | --- |
| D5-16 | Medium, harness | synchronous native output can retain descendant handles and had no fixture-level deadline | synthetic parent exits 7 while descendant survives; file-backed bounded execution preserves output/code; stalled process refuses at deadline | Regression passed in run 39107697740. Earlier long-job cause remains unproven; do not conflate with a product fix. |
| D5-17 | Medium, provider availability | Edge's deeper post-restart tree reaches unchanged depth cap | exact signed browser, both EXE architectures, no personal profile; attempted provider withdrawn and scanner disabled | Resolved by unsupported scope, not an implemented Edge solution; actual refusal subsequently passed in run 39108878064. |

Full 3-repeat resource evidence is uploaded in each performance job. A single full-report annotation was truncated at
GitHub's additional 4096-character limit in run 39107697740; next iteration splits bounded chunks. No missing third
repeat is reconstructed. A symmetric five-switch event-to-visible measurement was added **after**, separately
from, each foreground resource sample for both EXEs; failed/missing samples remain explicit, not zero. Final exact-head
verification and downloaded release-byte results must supersede this intermediate chronology before publication.

## Final candidate evidence

Run [39108878064](https://github.com/coderunknow/Xtension/actions/runs/39108878064), exact source
`66da890620b87d3d3799488aba3c5ce95637334f`, completed **success**, all 12 repository jobs:

1. **Static inspection:** local Python API/source mirrors pass: 92 types, 202 StringIds/rows, 803 locals. Windows CI
   additionally ran the actual PowerShell source checker, compiler language-version and explicit-registration gates.
   No local Windows execution is claimed. MIT LICENSE is unchanged from baseline; both history logs are append-only.
2. **Automated regressions/builds:** **101 core tests** (default AnyCPU build executed under PowerShell 7 and 5.1, not separate architectures), **19 synthetic
   Windows tests per architecture**, actual Framework 4.8/C# 5 x86+x64 EXEs, MSBuild project build. New regressions
   exercise identical-bounds/different-identity close refusal, press cancellation, settings batch/I/O atomicity,
   ownership, future-key backup, watcher re-subscription, import-result persistence, per-lane metrics, resource
   teardown and named-event single instance. These establish the relevant D5-01–15 source fixes; hooks/cap filters
   still have source-only coverage where no dedicated stress test exists.
3. **CI/package/provenance/performance:** package negative/provenance/checksum tests and file-backed fixture regression
   passed. Baseline dev4 download matched source `15900f524951b14ec7658e2ddadf07166f10ed3a`, product `0.1.0-dev4`,
   SHA-256 `35b5a67b2c416666b61d43dc1451e8e17c49c081c65226ec5e8cea407c6cce82`. Performance used one Windows
   OS `10.0.26100.0`, four logical processors, x64 process, the same pinned Brave, fresh three-blank-tab profiles,
   default settings, three alternating repetitions, 20 seconds per state/binary after 3-second warmup. CPU is a
   percentage of one core, not divided by processor count. All 12 resource observations and six reaction summaries
   were retrieved from three complete bounded annotations, not inferred from a truncated report.
4. **GUI/real browsers:** Brave `155.1.97.56` and signed Chrome `155.0.8059.40`, x86+x64 shipped candidate EXEs, passed
   actual visible placement, 24 rapid switches preserving tabs, two windows, focus away/back, modifier refusal,
   exact runtime-identity single-tab close/cooldown, minimize/restore/move/resize/maximize, many tabs, synthetic long
   title, hover visibility, configured 40 px fit and restart/corrupt-settings recovery. Chrome default-disabled
   refusal passed before opt-in. Signed Edge `154.0.4258.62` passed **refusal only** in both architectures: no tree walk,
   selected target or overlay even when the settings file requests it. Firefox not executed/supported. Physical
   mixed-DPI/multiple monitors, native x86 OS, true hung-provider behavior, startup-registry/Shell integration and
   human icon/diacritics/audio review remain outside these jobs. No mandatory owner session is a release gate.

### Complete repeated resource observations

Units: milliseconds, one-core CPU %, MiB. Resource windows were 20.0–20.6 s. “Scan” is the application's last
statistics line for that run (p50/p95/max); separate harness environmental scans are not mistaken for product latency.
Idle scan/counter timing is **n/a**, not the harness's empty-list `0.0`. Zero refusals/stales/discards/errors/stalls
were recorded for every foreground product sample; queue p95 was 0–1 ms, retained completed samples 28–31.

| State | Repeat | Binary | Ready | CPU | Working/private | Handles | Product scan p50/p95/max |
| --- | --- | --- | ---: | ---: | --- | ---: | --- |
| Idle | 1 | dev4 | 114 | 0.08 | 33.5/25.0 | 325 | n/a |
| Idle | 1 | candidate | 205 | 0.00 | 33.8/24.9 | 325 | n/a |
| Idle | 2 | dev4 | 115 | 0.08 | 33.5/24.9 | 325 | n/a |
| Idle | 2 | candidate | 204 | 0.08 | 33.8/25.0 | 325 | n/a |
| Idle | 3 | dev4 | 213 | 0.00 | 33.5/25.0 | 325 | n/a |
| Idle | 3 | candidate | 119 | 0.08 | 33.8/25.0 | 325 | n/a |
| Foreground | 1 | dev4 | 205 | 3.74 | 54.3/38.4 | 422 | 67/73/129 |
| Foreground | 1 | candidate | 209 | 4.34 | 54.3/38.3 | 421 | 67/90/99 |
| Foreground | 2 | dev4 | 218 | 4.27 | 54.4/38.4 | 421 | 69/96/100 |
| Foreground | 2 | candidate | 214 | 3.97 | 54.4/38.3 | 421 | 70/93/103 |
| Foreground | 3 | dev4 | 204 | 3.89 | 53.9/38.3 | 422 | 70/88/104 |
| Foreground | 3 | candidate | 210 | 4.86 | 54.4/38.4 | 421 | 69/98/107 |

After each foreground CPU/memory window, five native Ctrl+Tab inputs were timed to observed changed-overlay bounds,
then checked against the newly selected runtime identity and exact geometry. All **30/30** reaction observations
succeeded. Nearest-rank p95 of five equals that repetition's maximum; these are coarse harness observations sampled
about every 10 ms, not physical display latency, UIA-event delivery latency alone or close-validation latency.

| Repeat | dev4 n / p50 / p95 / max ms | Candidate n / p50 / p95 / max ms |
| --- | --- | --- |
| 1 | 5 / 103 / 115 / 115 | 5 / 101 / 119 / 119 |
| 2 | 5 / 100 / 116 / 116 | 5 / 106 / 126 / 126 |
| 3 | 5 / 110 / 115 / 115 | 5 / 107 / 115 / 115 |

**Interpretation, not a speedup claim:** startup ranges overlap (idle dev4 114–213 vs candidate 119–205 ms;
foreground 204–218 vs 209–214). Foreground median CPU is 3.89 vs 4.34% of one core, and median reported scan p95
88 vs 93 ms; candidate values are not an improvement. Ranges overlap and three short shared-runner repetitions
cannot establish a statistical regression or cause. Extra diagnostics work exists in source, but is not profiled as
the cause. Working/private memory and handles are comparable in these observations (idle working set +0.3 MiB in
candidate). Reaction p50 ranges overlap 100–110 vs 101–107 ms, while candidate p95 reaches 126 vs 116 ms. No claim
of measured latency improvement, zero overhead or universal non-regression is made. Follow-up: longer repeated
controlled desktop sampling/profiling before optimizing diagnostics; never loosen freshness/deadlines for speed.

### Final source and publication gates

The recorded successful run is the full implemented candidate, not a cancelled/incomplete run. A final test-only
improvement now explicitly targets x86 and x64 core EXEs under both PowerShell hosts; its pending exact-head CI must
confirm both architectures, rather than retroactively attributing that coverage to the earlier AnyCPU run. Subsequent evidence
wording/docs changes require another exact-head PR CI. Its result, normal merge SHA, merged-main CI, immutable tag,
release workflow and downloaded release-byte hashes are recorded on [PR #6](https://github.com/coderunknow/Xtension/pull/6)
and in the final delivery report to avoid claiming a future result or a self-referential commit hash. D5-13's publication
workflow implementation is not end-to-end accepted until that release actually completes. No old tag/asset is replaced.
