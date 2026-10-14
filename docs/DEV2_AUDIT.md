# dev2 audit — 2026-10-14

## Evidence boundary

Baseline: `804a256c273e830a6e2e862e3775fbf5596354b6`, main CI [38570045403](https://github.com/coderunknow/Xtension/actions/runs/38570045403) passed. The Linux authoring host cannot run .NET Framework, WinForms or Brave-on-Windows. Windows CI is used for real compilation, synthetic UIA/layout regressions AND an isolated real-Brave test of the actual built EXEs. Neither is a substitute for all manual desktop scenarios. No actual user exception, HRESULT, stack, Windows/Brave version or accessibility tree was supplied.

Reported message: “Quét UI animation thất bại, đã ẩn nút x”. Source wording: “Quét UI Automation thất bại. Đã ẩn nút ×.” This maps to the scanner's unexpected-exception catch, not a geometry or selection rejection.

## Findings / corrections / regression guards

| Severity | Finding and concrete condition | Correction / guard |
| --- | --- | --- |
| High | UiaTabScanner uses cache-less TreeWalker navigation then Cached.ControlType; an active CacheRequest does not apply to these overloads. | Pass the per-scan cache to first-child AND next-sibling. Windows fixture reproduced InvalidOperationException / 0x80131509 at Cached.ControlType, then confirmed the corrected scan and selected siblings. Actual Brave also passes. Upstream evidence: dotnet/wpf TreeWalker.cs and AutomationElement.Navigate (null request uses DefaultUiaCacheRequest). |
| High | Close timeout previously depended on Tick; a delayed message loop could accept a result before timeout processing. An overlay completion could re-show while close validation was pending. | Pure request-age deadline in CloseGuard, active request identity/epoch and pending-close suppression; core exact-deadline regression. |
| High | Overlay acceptance relied on last timer foreground rather than current HWND, and slow results could renew freshness at delivery. | Fresh HWND/identity/minimized/bounds/DPI checks; age measured from request creation, not completion. Brave smoke validates focus/windows/geometry; adversarial timing stress still needs manual acceptance. |
| High | One selected tab plus a tab with unreadable selection was called unique; reaching traversal depth or final slow call could still be considered complete. | Reject unknown selection and all exhausted traversal budgets; retain original limits and leaf skips. Core unknown-selection test; synthetic and real-Brave UIA traversal tests. Depth/final-call limit conditions are enforced in source but not individually fault-injected. |
| High (privacy) | Scanner and worker append arbitrary provider exception.Message to persistent logs. | Operation/type/HRESULT and bounded application-method stack only; no raw message/Data/inner exception/file paths. Core synthetic private-message regression. |
| Medium | Minimum accepted height was 16 px, but the overlay was 26 px; a valid-looking tab could produce an overlay outside its tab. | Require the whole square to fit. Tests at 96/120/144/192 DPI, exact fit and negative-monitor coordinates. No recognition threshold relaxation. |
| Medium | ShowAt ignored SetWindowPos failure and unchanged bounds skipped native topmost/show verification. | Check return, native visibility and physical bounds, hide on failure, surface safe status; Windows repeated-show/invalid-bounds test. Actual overlay native visibility, position and mouse click also pass real-Brave smoke; arbitrary compositor occlusion remains unverified. |
| Medium | Cached HWND identity omitted PID; transient negative identity survived 5 seconds. | Query PID before cache hit, do not reuse negative verdict, bypass cache for close. Native HWND/PID reuse stress unverified. |
| Medium | Fixed dashboard rows/notice height and fixed diagnostics labels could clip wrapped Vietnamese text; errors/timings did not invalidate diagnostics rendering. | Measured/scrolling dashboard, auto-height action rows, selectable title, scrollable status/error fields; complete signature. Synthetic scaled-font and changed-error tests. The first run actually caught a checkbox clipping at 200% text/narrow width; it passed after constrained wrapping. Physical display/diacritics acceptance unverified. |
| Medium | Mutex creation failure allowed duplicate instances with independent cooldowns. | Refuse startup when exclusion cannot be obtained. Registry/OS failure injection not performed. |
| Low | Worker/listener wait handles and tray-owned icon/menu resources were not disposed. | Owner-thread finally cleanup; idempotent worker stop and synchronized wake. Hung UIA stays bounded to the two existing background lanes; no replacement-thread proliferation. |
| Medium (build) | Get-FrameworkDirectory required Framework64 even on supported 32-bit Windows. | Fall back to Framework; runtime test on a 32-bit OS remains unavailable (x86 process tests on x64 CI are distinct). |

## Audit coverage and deliberately unverified risks

- Read all source, tests, scripts, manifest, project, workflows and contributor/history docs. No dependency added, architecture remains .NET Framework 4.8 / WinForms / C# 5, MIT unchanged. All new src files are explicit project compile items.
- Geometry remains a conservative top-strip heuristic, not proof of all Brave versions' tab semantics. Pinned/narrow/scrolled/vertical layouts remain unsupported or rejected. No speculative fallback to focus, titles or arbitrary Ctrl+W.
- Residual **high-impact architectural risk**: tab/focus/modifier state can change in the unavoidable gap after validation and before SendInput; existing documented limitation, not a new guarantee of atomic closure. Multi-selected/grouped/updated Brave trees require real acceptance. Held Ctrl behavior remains as before. Partial SendInput recovery is best effort.
- **Medium availability limitation**: a permanently blocked UIA provider cannot be safely aborted inside this design. At most two background threads remain; tray stays responsive, overlay hides, restart may be required. No infinite retries/replacement threads.
- **Medium unverified**: adversarial rapid-click/tab-switch races, physical mixed-monitor DPI transitions, native 32-bit Windows, pinned/vertical/scrolled layouts, tray/startup/graceful shutdown and Vietnamese visual rendering. Synthetic scaled fonts do not certify actual DPI transitions. The test forcibly cleans up its own app/browser processes, so it does not prove graceful exit. No screenshots/private logs committed.
- **Low, source-inferred diagnostic wording edge case**: `ActiveTabController.Tick` chooses the “Brave minimized” idle message from `minimized` without also testing `isBrave`. If Windows retains a minimized non-Brave HWND as foreground, the overlay still hides safely but the explanation can name the wrong application. Not reproduced with a non-Brave window; no close-safety impact. Left as a small follow-up rather than changing an already-published immutable binary: gate that wording on `isBrave && minimized` and add a deterministic status-text regression in the next patch.
- **Low**: startup registry status detects presence, not whether the executable was moved; settings errors fall back to defaults and save failures are logged. Temporary settings writes are atomic on successful replacement; cleanup on failed replacement and bounded parsing of a maliciously huge local file are follow-up hardening, not a proven browsing-path defect. Existing roundtrip/malformed settings tests retained.
- **Medium tooling defect fixed**: baseline packaging could reuse stale dist binaries. Builds now emit commit/version/architecture/hash/dirty-tree provenance; packaging checks it, binary version and PE machine. CI rejects five invalid-stamp cases and verifies package checksums. Release workflow re-downloads published assets for hash verification. No generated binaries or profiles are committed.
- No unavailable historical quotations were invented. Append-only logs preserve earlier records, including historical statements superseded by current remote evidence.

## Verification / publication gate

Observed green run: [38575630606](https://github.com/coderunknow/Xtension/actions/runs/38575630606), exact source `e382a8aeef2dfc40df10247c138f0f318a859451`. PR [#2](https://github.com/coderunknow/Xtension/pull/2) remains open/unmerged; its latest CI is the final-candidate authority. Later documentation/workflow changes are rechecked before tagging.

| Command | Observed result on Windows CI |
| --- | --- |
| `./scripts/verify-source.ps1` | Passed |
| `./scripts/test.ps1` | Passed, 38 tests |
| `./scripts/build.ps1 -Arch x86` and `-Arch x64` | Passed, C# 5 / Framework compiler |
| `msbuild Xtension.csproj /restore /p:Configuration=Release /p:Platform=x64` (plus logging flags) | Passed |
| `./scripts/test-windows.ps1 -Arch x86` and `-Arch x64` | Passed, six tests each |
| `./scripts/test-package.ps1` | Passed, valid package/checksums plus five rejected invalid provenance cases |
| `./scripts/test-brave.ps1 -Arch x86` and `-Arch x64` | Passed, actual EXEs; official browser ZIP checksum verified |

Real-Brave environment: release v1.97.56, binary ProductVersion 155.1.97.56, Windows runner OS 10.0.26100.0, 96 DPI, x64 OS/browser, both x86/x64 Xtension processes. Three isolated blank pages initially; no personal profiles. Primary recognition reports 64 UIA nodes. No force-renderer-accessibility flag or changed recognition thresholds were needed.

| Acceptance scenario | Coverage |
| --- | --- |
| Start Xtension with Brave already running; show button | Passed, actual EXEs |
| Switch tabs repeatedly; overlay follows | Passed, three switches |
| Click closes intended tab only | Passed; selected UIA runtime ID removed, other IDs survived, tab count stable afterward |
| Shift, Alt, Win safety | Passed; all three fixture tabs survived modifier clicks |
| Multiple Brave windows and other application focus | Passed; correct overlay follows two windows, hides outside Brave and recovers |
| Minimize/restore/maximize/move/resize | Passed |
| Many tabs / long title | Passed; twelve tabs and 300-character synthetic title |
| UIA failure/recovery/epochs | Synthetic legacy cache failure, injected worker error/recovery and stale epoch rejection passed; real hung provider not exercised |
| Layout at 100/125/150/200% text | Automated bounds assertions passed at small/normal sizes; physical DPI/diacritics visual inspection not performed |
| Rapid physical clicks / switch during close validation | Core exact-click/cooldown/deadline tests passed; real stress not performed. Brave's native close buttons were not hidden in the fixture, so this is not evidence of native-button-setting behavior |
| Tray, startup registration, settings, graceful exit | Core settings tests passed; full runtime acceptance not performed |

Publication assessment: the cache cause is established by actual Windows reproduction plus real-Brave success with both EXEs; this supports a prerelease, not universal acceptance. No new known critical/high implementation defect remains from this audit. Existing non-atomic input risk remains explicit. Tag only the final green candidate after source/version/clean-tree/tag-absence checks. No PR merge.

External reviews are NOT green reviews: Kilo reports ACTION_REQUIRED/out-of-credits, Qodo's trial ended, Copilot quota was exhausted and CodeRabbit skipped. These are optional review services, not jobs in repository CI. Main metadata reports unprotected/empty required contexts; repository ruleset listing is empty. Required-status administrative endpoint returned HTTP 403; no protections, reviews or checks were modified or bypassed. Owner review remains recommended before merging.

## Published result

- **Release:** [Xtension v0.1.0-dev2](https://github.com/coderunknow/Xtension/releases/tag/v0.1.0-dev2), published 2026-10-14T14:01:35Z; prerelease, not draft, not latest.
- **Exact release source:** `816d7664b79d8927731305c8f974533d65bae714` ([tag tree](https://github.com/coderunknow/Xtension/tree/v0.1.0-dev2)). Tag was not moved or replaced.
- **Final candidate CI:** [38576245878](https://github.com/coderunknow/Xtension/actions/runs/38576245878), all seven repository jobs passed, including PS5.1 parity and real-Brave x86/x64.
- **Release workflow:** [38576648574](https://github.com/coderunknow/Xtension/actions/runs/38576648574), all jobs passed, including downloading published assets again and validating hashes. GitHub REST asset digests match the verified hashes below.
- **Review branch:** `arena/8f3060a9-xtension`, PR #2 open and unmerged. Post-release history/verification updates on that branch do not alter the released source/tag.

| Asset | Bytes | SHA-256 |
| --- | ---: | --- |
| `Xtension-v0.1.0-dev2-win-x86.exe` | 74752 | `cfedbd004ce7cc5b68db937ea31c4a3115257aedf772e3a95a360f1cb66f3ce5` |
| `Xtension-v0.1.0-dev2-win-x64.exe` | 73728 | `25c836137928a274380fd63d2650499df06b92103c4ef30e834c907a5620dc9c` |
| `Xtension-v0.1.0-dev2-source.zip` | 107111 | `450cc1b214cdba0c72a32872c830dee305a72de0f7a0858ff8bd1f00d9604122` |
| `SHA256SUMS.txt` | 296 | `20311a8959fab00832e8c754668b13c2167b514665dc48cce06159ee0e4d6e95` |

Post-publication actual-binary smoke can be requested with `gh workflow run ci.yml --ref arena/8f3060a9-xtension -f published_tag=v0.1.0-dev2`. It checks out tag source, verifies downloaded EXE checksums and runs those published bytes against isolated Brave instead of substituting a newly compiled app. Its result is recorded in the append-only work log after completion.

### Actual downloaded release EXEs: additional acceptance

[CI 38579364444](https://github.com/coderunknow/Xtension/actions/runs/38579364444) passed all seven jobs. The [x86 job](https://github.com/coderunknow/Xtension/actions/runs/38579364444/job/115905008479) and [x64 job](https://github.com/coderunknow/Xtension/actions/runs/38579364444/job/115905008568) each verified source equivalence to the release tag, downloaded the actual published EXE, verified the SHA-256 above, substituted that file for the local build and passed the full real-Brave smoke. Application source in the review branch remains identical to the released tag; later changes are verification/history only.

Two post-release test runs had failed an invalid minimize expectation. With two browser windows, minimizing one can legitimately foreground the other; additionally, this Windows runner can retain an iconic Brave HWND as foreground. The corrected fixture first minimizes the secondary window and then requires both windows iconic and no visible overlay. Native diagnostics in BOTH successful architecture jobs showed `foregroundIsPrimary=True`, `foregroundIconic=True`, `foregroundBrave=True`, `priorOverlayVisible=False`, `visibleSmallWindow=0`. Thus the app correctly hid the overlay, while the earlier test's requirement that the foreground process cease to be Brave was wrong. Failed runs and corrections are retained in AGENTS_LOG; no application assertion was bypassed and no published tag/asset changed.

The integration denied optional manual dispatch with HTTP 403 `Resource not accessible by integration`. Normal PR CI completed equivalent published-byte verification without changing permissions: an existing VERSION-matching release is used only if its application/build-input source exactly matches the candidate. Otherwise CI tests the newly built candidate instead. Optional third-party review-service quotas remain separate from the successful repository CI.

Repeated verification: [CI 38580406511](https://github.com/coderunknow/Xtension/actions/runs/38580406511) also passed all seven jobs, including the same checksum-verified published EXEs and native minimize evidence. Thus the corrected published-binary harness succeeded in two consecutive full runs. No application-source changes were made after tagging.
