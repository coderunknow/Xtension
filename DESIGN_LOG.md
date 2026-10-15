# DESIGN_LOG.md — append-only source of truth

## Entry 001 — Original user intent

> Sửa lại thành v0.1.0. Đôi tên thành Xtension. Nâng cấp source code, tăng tốc độ, tối ưu và giảm tài nguyên tiêu thụ, tăng tính bền vững. Thử kết nối github push và tag release đa nền tảng [https://github.com/coderunknow/Xtension](https://github.com/coderunknow/Xtension) đảm bảo production ready, clean code.

## Entry 002 — Decisions

- Product and assembly identity: `Xtension`; semantic version: `0.1.0`, initial tag: `v0.1.0`.
- Preserve the app's Windows-only design because it depends on Win32, Windows Forms, Windows UI Automation, and `SendInput`. Provide architecture-specific Windows x86/x64 builds rather than claiming unsupported macOS/Linux compatibility.
- Keep the .NET Framework 4.8 target for compatibility with the existing source and simple local build path.
- Split application classes into focused source files without changing the safety model.
- Use a bounded 650 ms poll interval to lower repeated UI Automation scans; close-click handling still rescans the active tab immediately.
- Reduce scanner allocations by removing the per-node wrapper object and per-container child list. Use `StringBuilder` for diagnostics signatures.
- Fail closed when the active tab cannot be uniquely confirmed. Do not weaken this condition to improve apparent availability.
- CI compiles on Windows runners. A GitHub Release is triggered only by a pushed `v*` tag after the release workflow starts; this Linux environment cannot validate Windows UI behavior directly.

## Entry 003 — Progress and verification

- Done: renamed local project/source metadata and user-facing product strings to Xtension; centralized v0.1.0 and runtime settings; split source files; updated build scripts and manifest; wrote README/CHANGELOG; added CI and tag-release workflows; added a static consistency check.
- Done: inspected the old UIA scanner and preserved its maximum depth, node count, sibling guard, document-skip behavior, fail-closed selection rule, and fresh scan before sending Ctrl+W.
- Not done / blocked: Windows compilation and UI Automation runtime testing cannot be executed in this authoring environment. The production-ready claim remains gated on CI + manual Windows acceptance.
- Not done / pending connection result: push the repository contents and attempt to create the requested `v0.1.0` tag/release through the available GitHub connector actions; if tag/release creation is unsupported by the connector, report the exact boundary instead of claiming success.

## Entry 004 — Maintenance rule

Append new dated entries below; do not rewrite or delete prior entries. Preserve user intent and clearly distinguish done, not done, and blocked work.


## Entry 005 — GitHub write attempt

- 2026-10-14: repository metadata confirmed `coderunknow/Xtension`, default branch `main`, push permission reported by metadata, and existing MIT license.
- The available GitHub integration returned HTTP 403 `Resource not accessible by integration` for Git tree creation, branch creation, and Contents API file creation. No remote mutation occurred; no branch, source commit, tag, or release was created by the assistant.
- The authoring container cannot resolve `github.com`, so direct `git push` is unavailable here too.
- Added `publish-to-github.bat` for the user's authenticated Windows clone. It performs source validation, refuses an existing `v0.1.0` tag, pushes `main`, then creates/pushes the tag so the committed GitHub Actions release workflow can build/publish assets.


## Entry 006 — Release request for v0.1.0-dev1 (2026-10-14)

Verbatim user request (Vietnamese, unedited):

> Cho prompt để AI có kết nối github tiếp tục nâng cấp mã nguồn, thêm DESIGN_LOG, AGENTS_LOG,... và tiến hành push và tag release. Version: v0.1.0-dev1. đảm bảo hoạt động tốt, nhanh và mọi thứ hoàn hảo.

Decisions recorded from this request and the standing requirements:

- Version is `0.1.0-dev1` everywhere. The single source is the `VERSION` file. Tag `v0.1.0-dev1`. GitHub Release title `Xtension v0.1.0-dev1`. The release is a prerelease and is never marked latest.
- The overlay is a × on the selected horizontal Brave tab only. It moves with the tab and is hidden whenever the active tab is not confirmed.
- Brave's native close buttons are hidden through Brave's "Always hide tab close button" setting, if the installed Brave version provides it. This label is not confirmed in any source gathered so far, so the UI describes it as "Brave's tab close-button setting, if your Brave version provides it". Xtension never changes Brave's settings.
- Disabling the overlay and exiting the app are separate actions. Both warn that native close buttons may stay hidden if the Brave setting is still on.
- Startup with Windows is off by default. It is a per-user Run entry.
- Fail-closed rule: no keystroke unless a fresh scan of the active window confirms one selected horizontal tab and the click lands on its ×. Uncertain identity or geometry hides the button.
- Residual risk, documented and not mitigated: a tab switch in the roughly 100 ms between validation and keystroke cannot be prevented by any available API.
- Platform scope: Windows x86 and x64 only. No macOS or Linux claims. No placeholder binaries.
- Performance: no percentage claims. Diagnostics shows nodes visited and scan milliseconds so the on-device numbers can be measured later.

Status: source written and statically checked, not compiled or run in this environment. Nothing is claimed as tested until CI passes. GUI and Brave runtime behaviour is unverified.

Open item: the six earlier Vietnamese request quotes that the release plan calls for could not be restored verbatim from the session record. Only the original intent quote (Entry 001) and the request above are recorded here. Add the missing quotes by appending a new entry; do not edit this one.


## Entry 007 — Release v0.1.0-dev1 published as prerelease (2026-10-14)

Decisions and outcomes, recorded without editing earlier entries:

- Tag `v0.1.0-dev1` points to commit `8774575`, which passed CI run 38568637454 (static checks, unit tests, MSBuild check, x86 and x64 builds). The release workflow (run 38568996101) then published the GitHub Release titled `Xtension v0.1.0-dev1` as a prerelease, not a draft, and not latest.
- Published assets: x86 EXE, x64 EXE, source ZIP, and `SHA256SUMS.txt`. The release notes are the CHANGELOG section for 0.1.0-dev1 plus `docs/RELEASE_CAPABILITIES.md`.
- What is verified: the C# source compiles with the .NET Framework csc in CI, the pure-core unit tests pass in CI, and the release pipeline runs end to end.
- What is not verified: runtime behavior in Brave, the overlay's placement and click-to-close, DPI scaling, the Brave setting interaction, and tray and dashboard behavior. The Brave setting "Always hide tab close button" is not confirmed in any source gathered.
- Performance claims: none. Measurements are still needed on a device.
- Open items: the six earlier Vietnamese request quotes are still not recorded verbatim (the owner must supply them). PR #1 is open for review; its merge is the owner's decision.

## Entry 008 — dev2 audit and runtime-failure lead (2026-10-14)

Original request, verbatim:

> Cho prompt AI fix lỗi [https://github.com/coderunknow/Xtension](https://github.com/coderunknow/Xtension)
> Là không thấy nút x hiện ra dù đang ở đúng tab.
> Và khá nhiều vấn đề khác.
> Cho prompt để AI build v0.1.0-dev2 để đảm bảo bug-free.
> Hỏi tôi trước khi cho prompt.

Subsequent clarification, verbatim:

> Các câu trả lời để chuẩn bị prompt Xtension v0.1.0-dev2:
> 1. Các lỗi cần xử lý: AI tự audit toàn bộ repository, tìm và sửa các lỗi khác ngoài lỗi nút × không xuất hiện.
> một số chữ bị che, hiện tại không có vấn đề nhiều nhưng lâu dần có thể gây technical debt khá rủi ro.
> 2. Thông tin chẩn đoán: chuẩn đoán trống.
> 3. Phạm vi bàn giao: Sửa, test, build; sau khi đủ điều kiện tạo tag v0.1.0-dev2 và GitHub prerelease; không merge PR.
> 4. Kiểm thử: Tự động và kiểm thử thực tế Brave trên Windows nếu môi trường cho phép.
> Hãy viết prompt AI chất lượng cao cho Xtension v0.1.0-dev2 theo đúng các câu trả lời này, kiểm tra repository context đã biết, không hỏi lại thông tin đã cung cấp.

Additional reported observation, verbatim:

> In Xtension's Diagnostics window, the application displays: “Quét UI animation thất bại, đã ẩn nút x” (the exact displayed wording and capitalization must be verified against the actual application).

- Checkout baseline is main `804a256c273e830a6e2e862e3775fbf5596354b6`; remote PR #1 is now merged, unlike earlier historical notes. The existing dev1 tag/release and CI are real. This session works only on `arena/8f3060a9-xtension`; no merge is authorized.
- Actual source wording is `Quét UI Automation thất bại. Đã ẩn nút ×.` The catch in UiaTabScanner sets Error; the worker posts it, the controller hides the overlay, and Diagnostics displays Message and Error. No user exception type, HRESULT, stack, Brave version or Windows/DPI configuration was supplied. Those are NOT inferred from the generic message.
- Concrete defect: TreeWalker.GetFirstChild/GetNextSibling without the CacheRequest argument use the default cache, not the active request. The scanner then reads Cached.ControlType (and tab properties). Inspected upstream dotnet/wpf TreeWalker.cs and AutomationElement.Navigate via GitHub API: null navigation request maps to DefaultUiaCacheRequest. Pass the explicit per-scan request on both navigation calls, retain the ControlView, MTA worker, leaf exclusions, and original limits. Add a real Windows synthetic UIA test that reproduces the old exception and exercises the corrected scanner; this is not Brave acceptance.
- Bound enforcement: depth truncation previously looked complete; a slow final navigation escaped the elapsed-budget check. Both now fail closed. Element disappearance is classified as incomplete, retaining safe operation/error metadata.
- Privacy: provider exception messages can include browsing data. Persistent diagnostics now use operation + exception type + HRESULT + up to four application method frames, never arbitrary message/Data/inner exception text or stack paths. No actual provider message is retained, even in the diagnostic UI. Counts and operation are preserved on failure.
- Safety: accept results only inside a request-age deadline, recheck the real foreground and physical window geometry/DPI on delivery, reject unknown selection alongside a known selection, require the overlay to fit fully inside the tab, and cancel pending overlay scans on a close click. Do not rely on UI timer scheduling to expire a close result. Preserve CloseGuard, fresh close scan, exact click, modifiers, epoch and cooldown. PID-aware identity caching does not cache failed queries; close identity bypasses the cache. Native placement failure is checked and surfaced, not treated as visible success.
- UI: retain hierarchy but replace fixed dashboard rows with measured wrapping text and auto-height flow rows inside a scrollable viewport. Long titles are selectable; diagnostics status/errors are read-only scrollable text, counters/error changes invalidate rendering, column headers are measured and tab titles have tooltips. Fit initial windows to the working area. Synthetic scaled-font/layout assertions are planned; physical DPI and Vietnamese visual rendering still require Windows desktop acceptance.
- Release gates: all real scripts/builds, C# 5, Windows x86/x64, MSBuild and final candidate CI must pass. No tag until adequate evidence supports the primary symptom; do not equate a synthetic fixture with Brave. No mathematical bug-free claim. Remaining risks and the final publication decision will be appended after observed checks.

## Entry 009 — dev2 evidence and prerelease decision (2026-10-14)

- Windows synthetic reproduction established the cache failure: `Cached.ControlType`, `InvalidOperationException`, HRESULT `0x80131509` in x86 and x64. Explicit cache navigation then confirmed the selected tab. This is actual fixture evidence, not an invented exception from the user's machine.
- Contrary to the initial local-environment limitation, GitHub Windows CI supports a real isolated Brave test. Run 38575630606 passed actual x86/x64 EXEs against checksum-verified official Brave v1.97.56, Windows OS 10.0.26100.0, DPI 96: visibility, tab switches, two windows, focus transitions, modifier rejection, physical mouse close with selected/unselected runtime-ID comparison, window geometry transitions, twelve tabs and a 300-character synthetic title. No private profile or screenshot was used/committed.
- First Windows regression run exposed 200% scaled-text checkbox clipping; constrained wrapping fixed it without removing the assertion. Dashboard fixed rows and diagnostics fixed labels were replaced by measured/scrolling layouts. Physical DPI and visual accent rendering remain unverified.
- Build provenance now prevents stale dist binaries from being packaged. Final release workflow will download actual published assets and verify all three SHA-256 entries plus the checksum manifest itself.
- Decision: source, synthetic and actual browser evidence adequately supports a dev2 prerelease after the final exact-commit CI gate. No new known critical/high implementation defect remains; do not claim complete acceptance or mathematical bug freedom. Keep the existing non-atomic validation-to-input risk and full remaining test matrix in docs/DEV2_AUDIT.md. Optional Kilo/Qodo/Copilot/CodeRabbit services did not perform reviews because of credits/quota/eligibility; this is disclosed, not marked passed. No protection bypass or merge is authorized. Publication still awaits final CI and tag checks at this entry.

## Entry 010 — dev2 published and downloaded assets verified (2026-10-14)

- Final candidate CI 38576245878 passed all seven repository jobs on `816d7664b79d8927731305c8f974533d65bae714`, including Windows PowerShell 5.1 parity and expanded actual-Brave tests for both architectures. Clean working tree, matching VERSION, absent target tag/release and open PR were checked before tagging. The local gh version lacks `release list --json`; the gate stopped without mutation, then used the Releases REST API successfully.
- Annotated tag `v0.1.0-dev2` (tag object `7497061528a895b79e4d7ef38dbecd45a1424abc`) was created once, pointing to that exact commit, and pushed. No tag was moved/deleted and no PR merged.
- Release workflow 38576648574 succeeded: tag verification, source/core checks, x86/x64 builds and Windows regressions, packaging/provenance/checksums, publication, and download/reverification of published files. Release https://github.com/coderunknow/Xtension/releases/tag/v0.1.0-dev2 is prerelease=true, draft=false. The latest-release API returned 404: no release is marked latest.
- Four assets are uploaded: x86 EXE (74752 bytes), x64 EXE (73728), source ZIP (107111), SHA256SUMS.txt (296). Re-downloaded artifact SHA-256 values agree with GitHub asset digests; details are in DEV2_AUDIT. This verification ran on Windows CI, not an invented local download.
- Post-publication verification adds an optional manual CI input that checks out the release tag and substitutes its downloaded, checksum-verified EXEs into the real-Brave smoke test. This changes CI/history only, never the published tag or app source. Its outcome is pending at this entry.

## Entry 011 — Published-byte runtime acceptance completed (2026-10-14)

- CI 38579364444 on review-branch commit `175369bd07c174747503a80187cd12beddd79851` passed all seven jobs. Both real-Brave jobs checked that application source matches the immutable release tag, downloaded the published EXEs, verified their exact published SHA-256 values, and then ran those bytes through the full smoke suite. No released application source or tag changed.
- Follow-up runs exposed a TEST expectation issue, not a demonstrated app minimize bug. The old multi-window fixture assumed that minimizing Brave necessarily changed the foreground process. Actual native diagnostics now show BOTH windows iconic, the primary minimized Brave HWND still foreground, prior overlay native Visible=false, and no visible small app window. This was observed for both architectures. Production's independent IsIconic check behaves correctly. The revised test retains the hide requirement and checks both iconic states; no safety assertion is skipped.
- Manual Actions dispatch returned HTTP 403, so the same stronger verification completed via ordinary PR-triggered CI with read-only content permissions. Optional third-party reviews remain unavailable. This adds evidence beyond the original release gate without rewriting history or the released artifact.
- Remaining acceptance gaps and inherent post-validation race remain as documented. No claim of universal bug freedom, every DPI/Brave version, native x86 OS testing or full tray/startup/graceful-exit acceptance is made.

## Entry 012 — Owner authorizes integration (2026-10-14)

New user instruction, verbatim:

> merge and complete the final things.

- This explicitly supersedes the earlier requirement to keep PR #2 open/unmerged. Earlier entries remain unchanged as historical records.
- Integrate PR #2 with a normal merge commit after checking the exact updated PR head's repository CI. Do not squash, rebase, use auto-merge, bypass protections, force-push, delete the session branch, or change the published release tag/assets.
- Verify the resulting main-branch CI and record the observed merge commit and verification links on PR #2. The PR merge record and final verification comment are the canonical integration outcome, avoiding a circular sequence of log-only merges.
- The dev2 release remains the immutable tested commit `816d7664b79d8927731305c8f974533d65bae714`. Integration does not imply that previously documented desktop acceptance gaps or optional external-review limitations are resolved.

## Entry 013 — dev3 comprehensive audit, Settings, hover mode and release (2026-10-15)

Original request, verbatim:

> Cho prompt toàn diện để AI nâng cấp [https://github.com/coderunknow/Xtension](https://github.com/coderunknow/Xtension) build thành v0.1.0-dev3.
> hỏi tôi tất cả câu hỏi cần thiết trước khi ra prompt.

Confirmed decisions and scope, verbatim:

> 1. Phạm vi: broad-evolution
> 2. Git workflow: pr-open
> 3. Phát hành: publish
> 4. Lỗi/tính năng/yêu cầu bổ sung: Tập trung vào tốc độ, lỗi tiềm tàng. Nâng cấp độ ổn định và tốc độ cho nhanh mượt nhưng tốn ít tài nguyên máy. Đồng thời thì thêm phần settting riêng cho x. Thêm các option thường có trong app và các option quan trọng (như khi con trỏ chuột di tới phần taskboard của task hiện tại thì mới hiện nút x, sửa size nút x,...).

Verbatim instruction received with the task:

> Update the DESIGN_LOG to suitable.

Clarifications answered by the owner before implementation (recorded verbatim as quoted options):

> hover_region: Tab rect ∪ × button rect + ~250 ms hide grace (recommended)
> extra_options: Required + a few advanced options
> ui_language: a toogle that changes between English and Vietnamese.
> manual_acceptance: Yes — before tagging, I'll test and report
> perf_evidence: Measure published dev2 EXE vs dev3 EXE in one CI job (recommended)

Decisions and design choices for this candidate:

- Baseline verified before editing: default branch `main`, tip `5917ded` (merge of PR #2), tags `v0.1.0-dev1` and `v0.1.0-dev2` present, both releases prereleases with no release marked latest, PR #1 and PR #2 merged, working tree clean. `VERSION` was `0.1.0-dev2`. Nothing from the dev2 baseline was reverted; the dev2 corrections (explicit UIA navigation cache, bounded-time fail-closed rules, freshness/epoch revalidation, privacy-safe diagnostics, measured layouts, provenance-gated packaging) are preserved and extended.
- The hover requirement is implemented for the selected **Brave tab**, not the Windows taskbar: eligibility is the union of the confirmed selected tab's rectangle and the overlay's own rectangle, plus a bounded hide grace (default 250 ms, configurable 0–1000 ms, advanced). Inactive tabs, Brave's title bar, the taskbar and other applications never satisfy it, because eligibility is derived only from a fresh `Ready` scan of the confirmed selected horizontal tab.
- Visibility modes: `always` (default, the established behaviour) and `hover`. Defaults change nothing for existing users; the persisted `overlayEnabled` key keeps its default `true`.
- The custom × size is a bounded logical-pixel value (18–40, default 26) and the right inset is 0–12 logical pixels (default 3). Values are clamped at load and at input; geometry, hit testing, painting and close validation all use the same effective values, so a size change cannot leave the native window or the validated click area at the old size.
- Language: a real toggle between Vietnamese (default, matching the shipped UI) and English, applied immediately to every user-visible string. All user-visible text moved into `src/Core/Strings.cs`; log lines and internal decision reasons stay English by design. Scan messages are stored as keys, not as localized text, so a language change re-renders immediately and stale localized text cannot survive in controller state.
- Settings live in Xtension's own `%LOCALAPPDATA%\Xtension\settings.ini`, never in Brave's profile or Brave preferences. Start-with-Windows stays a per-user registry `Run` entry, disabled by default, and the registry remains its single source of truth (the settings window reflects it, it does not duplicate it).
- Settings persistence hardening: in-memory cache (no re-read per frame/hover/scan), bounded file size, bounded integer parsing, enum validation with fallback, unknown/future keys preserved across rewrites, atomic replace with temporary-file cleanup on failure, and a reset-to-defaults action that persists and reapplies.
- Performance work is limited to measured or mechanically provable costs: idempotent overlay placement (no repeated `SetWindowPos`/repaint when the bounds did not change), PID-aware identity caching for negative verdicts, change-gated UI notification instead of one snapshot + render per timer tick, disposal of the per-scan `CacheRequest`, and hover-mode scan gating that can only delay showing the × (never authorize it). No safety check, validation depth, freshness rule, cooldown, deadline or single-instance guard is relaxed to gain speed.
- Release: candidate CI must pass on the exact commit, the owner validates the built EXE from that CI run before tagging, then tag `v0.1.0-dev3` on that same commit and let the existing tag-triggered prerelease workflow publish. PR stays open and unmerged; `main` is not touched. Residual risk, unmeasured metrics and untested desktop scenarios are recorded in `docs/DEV3_AUDIT.md` rather than claimed as accepted.

## Entry 014 — dev3 implementation record and release gate (2026-10-15)

Implementation decisions taken while building entry 013's plan, recorded so a later reader does not have to infer them
from the diff:

- **Pointer tracking.** Hover uses the existing UI timer, not a new high-frequency loop and not a global pointer hook. In hover mode the timer runs at 100 ms (always mode keeps the established 250 ms), which bounds the perceived latency of showing the button without adding a message-pump hook that Windows can silently remove.
- **Scan gating in hover mode.** While the button is hidden and the pointer is outside the window's tab strip, no UI Automation scan is submitted. A pointer outside the strip cannot satisfy any eligibility rule, so this can only delay showing the button; unknown pointer position or unknown geometry falls back to scanning. This is the main idle-CPU win of hover mode and it is deliberately a *delay-only* optimisation.
- **Change-gated notification.** The controller compares the observable fields it would report and notifies the views only when one changed, replacing a snapshot build plus render per 250 ms tick. `ForceNotify()` remains for changes that must repaint immediately (language, settings).
- **Negative identity caching.** A non-Brave foreground is cached as a negative verdict per window and process id with a shorter lifetime than a positive verdict, so the common "user is not in Brave" state stops opening a process handle 4 times a second. The close path still passes `fresh: true`.
- **Placement idempotence.** `ShowAt` returns immediately when the button is already visible at the same bounds with the same painted size, and still verifies the native bounds and visibility after every real move. A failed verification hides the button, as before.
- **Settings ownership.** `TrayAppContext` owns the settings object and is the single path for every change: apply to the controller, persist, refresh tray/dashboard/overlay. The settings window is a view over that path through `ISettingsHost`, which is what makes it exception-free about the tray and testable without one.
- **Start-with-Windows stays in the registry.** The settings window reads and writes the same per-user `Run` entry as the tray menu; it is not mirrored into `settings.ini`, so there is exactly one source of truth and no divergence to reconcile.
- **Language.** All user-visible text is a `StringId` in `src/Core/Strings.cs` with a Vietnamese and an English row; scan results store a message key instead of text so a language change re-renders the last explanation immediately. A static rule forbids Vietnamese text outside that file.
- **Hover sizing is not a new safety surface.** The configured size and inset flow through `TabGeometry.GetOverlayBounds`, the overlay window size, the hit-test rectangle and `CloseGuard`, from one value, so a change cannot leave validation using geometry the user is not looking at.
- **Resource work is bounded to provable costs.** No freshness window, deadline, budget, epoch rule, cooldown, stall threshold or confirmation step was weakened. The scan interval and hover keep-time are exposed as bounded settings instead of being silently made slower.

Release gate for this candidate, unchanged from entry 013: CI green on the exact commit that is tagged; the owner runs
the EXE built from that CI run on their own Windows machine and reports; only then is `v0.1.0-dev3` tagged on that same
commit and published as a prerelease by the existing tag-triggered workflow. The pull request stays open and unmerged,
`main` is not touched, and no published tag or asset is modified. Residual risk and unverified surfaces are listed in
`docs/DEV3_AUDIT.md`, including the surfaces that this candidate explicitly does not claim.
