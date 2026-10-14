# dev2 audit — 2026-10-14

## Evidence boundary

Baseline: `804a256c273e830a6e2e862e3775fbf5596354b6`, main CI [38570045403](https://github.com/coderunknow/Xtension/actions/runs/38570045403) passed. The Linux authoring host cannot run .NET Framework, WinForms or Brave-on-Windows. Windows CI is used for real compilation and synthetic UIA/layout regressions. It is not a substitute for browser acceptance. No actual user exception, HRESULT, stack, Windows/Brave version or accessibility tree was supplied.

Reported message: “Quét UI animation thất bại, đã ẩn nút x”. Source wording: “Quét UI Automation thất bại. Đã ẩn nút ×.” This maps to the scanner's unexpected-exception catch, not a geometry or selection rejection.

## Findings / corrections / regression guards

| Severity | Finding and concrete condition | Correction / guard |
| --- | --- | --- |
| High | UiaTabScanner uses cache-less TreeWalker navigation then Cached.ControlType; an active CacheRequest does not apply to these overloads. | Pass the per-scan cache to first-child AND next-sibling. Windows fixture reproduces legacy exception and checks actual corrected scan and selected siblings. Upstream evidence: dotnet/wpf TreeWalker.cs and AutomationElement.Navigate (null request uses DefaultUiaCacheRequest). |
| High | Close timeout previously depended on Tick; a delayed message loop could accept a result before timeout processing. An overlay completion could re-show while close validation was pending. | Pure request-age deadline in CloseGuard, active request identity/epoch and pending-close suppression; core exact-deadline regression. |
| High | Overlay acceptance relied on last timer foreground rather than current HWND, and slow results could renew freshness at delivery. | Fresh HWND/identity/minimized/bounds/DPI checks; age measured from request creation, not completion. Real timing/focus stress still needs Brave. |
| High | One selected tab plus a tab with unreadable selection was called unique; reaching traversal depth or final slow call could still be considered complete. | Reject unknown selection and all exhausted traversal budgets; retain original limits and leaf skips. Core unknown-selection test; synthetic UIA traversal test. |
| High (privacy) | Scanner and worker append arbitrary provider exception.Message to persistent logs. | Operation/type/HRESULT and bounded application-method stack only; no raw message/Data/inner exception/file paths. Core synthetic private-message regression. |
| Medium | Minimum accepted height was 16 px, but the overlay was 26 px; a valid-looking tab could produce an overlay outside its tab. | Require the whole square to fit. Tests at 96/120/144/192 DPI, exact fit and negative-monitor coordinates. No recognition threshold relaxation. |
| Medium | ShowAt ignored SetWindowPos failure and unchanged bounds skipped native topmost/show verification. | Check return, native visibility and physical bounds, hide on failure, surface safe status; Windows repeated-show/invalid-bounds test. No claim about actual compositor occlusion. |
| Medium | Cached HWND identity omitted PID; transient negative identity survived 5 seconds. | Query PID before cache hit, do not reuse negative verdict, bypass cache for close. Native HWND/PID reuse stress unverified. |
| Medium | Fixed dashboard rows/notice height and fixed diagnostics labels could clip wrapped Vietnamese text; errors/timings did not invalidate diagnostics rendering. | Measured/scrolling dashboard, auto-height action rows, selectable title, scrollable status/error fields; complete signature. Synthetic scaled-font and changed-error tests. Physical display/diacritics acceptance unverified. |
| Medium | Mutex creation failure allowed duplicate instances with independent cooldowns. | Refuse startup when exclusion cannot be obtained. Registry/OS failure injection not performed. |
| Low | Worker/listener wait handles and tray-owned icon/menu resources were not disposed. | Owner-thread finally cleanup; idempotent worker stop and synchronized wake. Hung UIA stays bounded to the two existing background lanes; no replacement-thread proliferation. |
| Medium (build) | Get-FrameworkDirectory required Framework64 even on supported 32-bit Windows. | Fall back to Framework; runtime test on a 32-bit OS remains unavailable (x86 process tests on x64 CI are distinct). |

## Audit coverage and deliberately unverified risks

- Read all source, tests, scripts, manifest, project, workflows and contributor/history docs. No dependency added, architecture remains .NET Framework 4.8 / WinForms / C# 5, MIT unchanged. All new src files are explicit project compile items.
- Geometry remains a conservative top-strip heuristic, not proof of all Brave versions' tab semantics. Pinned/narrow/scrolled/vertical layouts remain unsupported or rejected. No speculative fallback to focus, titles or arbitrary Ctrl+W.
- Residual **high-impact architectural risk**: tab/focus/modifier state can change in the unavoidable gap after validation and before SendInput; existing documented limitation, not a new guarantee of atomic closure. Multi-selected/grouped/updated Brave trees require real acceptance. Held Ctrl behavior remains as before. Partial SendInput recovery is best effort.
- **Medium availability limitation**: a permanently blocked UIA provider cannot be safely aborted inside this design. At most two background threads remain; tray stays responsive, overlay hides, restart may be required. No infinite retries/replacement threads.
- **Medium unverified**: real Windows/Brave startup, tab-switch/click races, minimize/restore/maximize, mixed-monitor DPI transitions, tray/startup/shutdown and Vietnamese visual rendering. Synthetic scaled fonts do not certify actual DPI transitions. No screenshots/private logs committed.
- **Low**: startup registry status detects presence, not whether the executable was moved; settings errors fall back to defaults and save failures are logged. Temporary settings writes are atomic on successful replacement; cleanup on failed replacement and bounded parsing of a maliciously huge local file are follow-up hardening, not a proven browsing-path defect. Existing roundtrip/malformed settings tests retained.
- **Medium tooling follow-up**: packaging verifies checksums but baseline does not prove existing dist binaries belong to current HEAD; candidate provenance hardening is under review before any publication.
- No unavailable historical quotations were invented. Append-only logs preserve earlier records, including historical statements superseded by current remote evidence.

## Verification / publication

Candidate CI pending. No new tag, release or PR merge yet. The release decision will be updated only from observed results.
