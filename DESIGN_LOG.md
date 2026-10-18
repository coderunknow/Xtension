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

## Entry 015 - Owner reports latency, directs the merge and the dev3 tag (2026-10-15)

Owner direction, quoted verbatim: "Ok but the delay is significant. So please make it as fast as possible cleanly. And
the setting is lag at the opening. Finally merge and tag release. Audit carefully. Make no mistakes."

- **The release gate changes here, on the owner's instruction.** Entries 013 and 014 made the owner's own Windows pass a
  precondition for the tag. The owner has now used the candidate on their own machine - the noticeable delay and the
  settings window lag are observations from that use - and asks for the release instead of another report cycle. The
  remaining gates stay: CI green on the exact commit that is tagged, the tag pushed on the merged commit, and no
  published tag, release or asset modified.
- **Speed is gained at the cause, never by relaxing a rule.** Three causes were found and removed: the hover scan waited
  out the interval that belonged to the previous state instead of scanning the moment the pointer reached the tab strip;
  a newly focused Brave window was not scanned until the next interval; and both windows in the UI were built row by row,
  re-measuring every wrapping label on every layout pass, with a `Font` object per control and an unconditional resize on
  show. The fixes are: an arrival in the tab strip forces one scan on that tick and the strip uses the documented minimum
  interval while the pointer stays inside it, a focus change scans immediately, and the windows build with layout
  suspended (one layout pass), measure each text once per text/width/font with invalidation on change, share fonts by
  role, and skip the fit-to-working-area resize when there is nothing to do.
- **The one visible delay that is deliberately kept** is the 800 ms close cooldown after a Ctrl+W: it is what stops a
  double click from closing the next tab, and the real-Brave job asserts it ("no consecutive close"). The owner asked for
  speed "cleanly", and removing a safety rule is not a clean speed-up. Likewise untouched: the 1500 ms staleness window,
  the 3000 ms close-validation deadline, the 1000 ms / 1500 node scan budget, the 5000 ms stall threshold and the epoch
  rule.
- **No new hard-coded cadence.** The fast path uses `AppInfo.MinScanIntervalMs`, which is the documented minimum of the
  user-facing `scanIntervalMs` setting, so the configured value still bounds what happens everywhere else and the
  behaviour stays explainable from the settings window.
- **Release path recorded before it was taken:** merge PR #3 with a merge commit (matching PR #1 and PR #2, which were
  merged the same way), push `v0.1.0-dev3` on the merged commit, and let the existing tag-triggered workflow build both
  architectures and publish the prerelease with the two EXEs, the source zip and `SHA256SUMS.txt`. Evidence for every
  step is recorded in `AGENTS_LOG.md`.

## Entry 016 — dev4 request: real-time ×, stability, browsers, platforms, icon, profiles, diagnostics (2026-10-16)

Owner request, quoted verbatim (abridged only where the prompt repeats itself; the full text is the task prompt):

> "Xtension v0.1.0-dev4 — Full Repository Audit, Real-Time ×, Stability, Multi-Browser, Platform Expansion &
> Verified Prerelease … 1. Fix the delay before the × appears and any defects that prevent it from appearing when the
> intended tab is safely identifiable. 2. Improve runtime stability, failure recovery, resource consumption…
> 3. Add useful configurable performance profiles, more informative diagnostics, appearance customization, and safe
> settings backup and restoration. 4. Conduct a repository-wide defect and maintainability audit… 5. Expand browser
> support beyond Brave… 6. Address the requested expansion to multiple desktop operating systems without breaking the
> existing Windows application or falsely advertising unsupported platforms. 7. Add a professional, recognizable
> application icon… 8. Verify all material claims through executable tests, GitHub Actions, real-browser smoke tests,
> and reproducible measurements. 9. Merge and publish `v0.1.0-dev4` as a prerelease only after the release gates …
> pass." — plus an additional issue: "audio from a video currently playing becomes briefly distorted or crackles
> whenever Xtension displays a notification popup window … Identify and verify the actual cause before changing the
> implementation … Do not claim the issue is fixed unless the result has been verified during actual audio playback."

**Baseline inspected before any edit** (entry 001–015 read in full; nothing rewritten):

- `main` at `50fbb2c9f93d1880f772fc6cc03fcf4fce8fa812` (merge commit of PR #3). Working tree clean. Only tag on the
  remote: `v0.1.0-dev3`. Published releases: `v0.1.0-dev1`, `v0.1.0-dev2`, `v0.1.0-dev3`, all prereleases, none latest.
- `VERSION` was `0.1.0-dev3`; the dev4 work starts from that commit, and dev4 branch is
  `arena/9ec9bbb9-xtension` branched from that commit.
- The dev3 candidate is the comparison baseline for measurements (`v0.1.0-dev3` published x64 EXE, checksum-verified by
  the existing performance job, which still pointed at `v0.1.0-dev2` and is corrected here).

**Decisions taken for dev4** (each with the reason it was chosen over the alternative):

- **Latency is attacked at the event, not at the interval.** dev3's fast path was still a *timer* path: an arrival in
  the tab strip or a new foreground window cleared the interval, but a *tab switch* had to wait out the configured scan
  interval (300–2000 ms, default 500 ms) before the × followed the selection. dev4 adds two bounded event sources —
  a UI Automation `SelectionItemPattern.ElementSelectedEvent` subscription on the confirmed browser window and a
  `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` notification — that only *ask for an earlier scan*; they can never
  authorise one, and polling remains the fallback when a provider denies, fails or stops sending events. The scan
  budgets, staleness window, close-validation deadline, epoch rule, cooldown and confirmation rules are unchanged.
- **The interval accounting bug is fixed rather than hidden.** `Tick()` advanced the "last scan request" timestamp
  before knowing whether the worker accepted the request, so a request refused because a scan was still running cost a
  full extra interval. The timestamp now advances only when the request is accepted.
- **The dashboard gets its own scan lane.** The dashboard's background scan of a non-foreground browser shared the
  overlay lane, and because a lane holds one request, a slow background scan could delay the scan that makes the ×
  appear. The dashboard now has a separate bounded lane (one thread, one pending request, same budgets).
- **Browsers become data, not a hard-coded `brave.exe`.** A `BrowserCatalog` maps executable names to a browser kind,
  a support level and a token; `ProcessIdentity` never matches a window by title or by an unverified substring, it
  queries the process image name exactly as before and now returns *which* supported browser the window belongs to.
  A browser is offered to the user only at or above the support level its acceptance evidence justifies, and the × is
  shown only for browsers the user enabled, so dev3 behaviour (Brave only) stays the default for an existing profile.
- **The platform boundary is documented as blocked, not simulated.** No macOS or Linux host can be built, run or
  verified with the tools available here (no runner has a GUI accessibility tree for a WinForms/Win32 host, and the
  product's tab confirmation depends on per-OS accessibility implementations). dev4 therefore ships no macOS/Linux
  code, adds a documentation entry with the exact blockers and the required work, and adds a static rule that fails if
  the documentation starts claiming those platforms. `AGENTS.md` keeps its "do not claim" rule.
- **Notifications stop being Shell balloons.** The notification surface becomes Xtension's own non-activating,
  sound-free window (created once, shown with `SWP_NOACTIVATE`, never activated) so showing a notification can no
  longer change the foreground window, play the system notification sound or create a Shell toast window. This is the
  minimal change that removes the mechanisms a notification popup can use to disturb audio playback, and it does not
  disable notifications.
- **Profiles are a marker over authoritative values.** `performanceProfile` records which preset the user last chose;
  the individual keys stay authoritative, so a user who edits a profile-controlled value keeps that value across a
  restart and is shown as `custom` instead of being silently reset. No profile can change a safety value; the profiles
  only select poll/scan cadences inside their existing documented ranges, and `balanced` equals the dev3 defaults, so
  an existing installation behaves identically until the user opts in.
- **Settings backup is the settings file, validated on the way in.** Import is parsed and bounded *before* anything is
  written, unknown keys are preserved exactly as the normal load path preserves them, the previous file content is
  restored if the write fails, and the result tells the user which values were applied, rejected or preserved.
- **The icon is a real asset with multi-size frames**, embedded in the executable as its application icon and loaded
  as the tray icon from the embedded resource with a painted fallback, so no image-processing dependency is added.

**Acceptance criteria recorded before implementation** (the release gates for this candidate):

1. Both Windows architectures build, static checks pass, the full core and Windows harness test sets pass on the exact
   tagged commit.
2. The real-browser acceptance proves *visible placement and an actual single-tab close* for every browser declared
   supported, with the browser version and the refusal behaviour on ambiguous or unsupported states.
3. A measured improvement of the event-to-visible path versus the published dev3 EXE, measured on one runner with the
   same method, and no measured regression in idle CPU, working set or handles beyond noise.
4. Every safety invariant is unchanged or stronger; a test demonstrates each fail-closed refusal still refuses.
5. Version, changelog, README, audit document and both append-only logs agree with the published artifacts.
6. The merge happens only after the required jobs are green, the tag is created on the merged commit, and the release
   workflow publishes a prerelease whose assets and checksums verify.

**Unresolved conflict recorded at this point:** the audio issue cannot be verified in this environment (no Windows,
no audio device, no browser playback), so the notification change above is implemented as a *candidate* fix with an
instrumented reproduction procedure and is explicitly reported as **not verified during real audio playback**.

## Entry 017 — dev4 implementation record and the small decisions taken while writing it (2026-10-16)

Appended after the implementation, before any CI run. Nothing here rewrites Entry 016; it records what was decided
while the code was being written, so the reasoning is not lost.

- **The event sources are advice, and the floor is measured in the code, not chosen for looks.** The selection-event
  watcher and the foreground hook can only set a flag that makes the next tick scan immediately; the flag expires after
  the staleness window, the worker still accepts one request at a time, and `EventWatchMinScanGapMs` (80 ms) is the
  floor between two event-driven requests. A provider that denies, hangs or stops sending events therefore costs
  responsiveness, never correctness, and a registration that hangs is abandoned after 1.5 s.
- **`ScanLane` replaced the ad-hoc "kind" flag.** One enum with three values (`Overlay`, `Close`, `Dashboard`) is the
  single description of why a scan exists; every counter, percentile and log field is per lane, so the dashboard's
  background work can no longer be mistaken for the × path, in the code or in the numbers.
- **The statistics line is bounded in both directions and never empty.** At most one line every 15 s or every 50
  completed scans, no line while nothing has been scanned, and the line carries `samples=`. This is what makes a 20 s
  measurement window (the CI resource job) contain real numbers, without letting a busy machine fill the log.
- **The EXE gets the icon as well as the managed resource.** `/resource:` makes the ICO loadable by `AppIcon` for the
  tray and the windows; `/win32icon:` (build scripts) and `<ApplicationIcon>` (csproj, for the MSBuild check) make
  Explorer, the taskbar and the file properties show the same asset. Both gates now fail if either flag disappears, so
  the icon cannot silently regress to the painted fallback.
- **The recorded `fast` and `balanced` cadences come from the constants, not from a wish.** `balanced` is exactly the
  dev3 defaults (500/250/100/2000 ms), `fast` uses the documented minima (300/100/50 ms, dashboard 1500 ms) and
  `low-resource` slows everything down (1200/500/250 ms, dashboard 5000 ms). The README table is the source a user
  reads, so it was corrected against `AppInfo` while writing it, and `custom` is documented as "your scan interval with
  the balanced poll intervals", which is what `Profiles.CadenceFor` returns.
- **Appearance is three bounded values, not a theme engine.** Colour is one of four named tokens, the border is a
  switch, and opacity is uniform window alpha. Alpha cannot change the hit area, which is why it was chosen over
  per-pixel blending, and 30% is the floor that still reads as a button.
- **`tests/WindowsTests/BrowserBoundary.cs` was not written, deliberately.** The contract it was meant to cover is
  already covered by `tests/UnitTests/BrowserCatalogTests.cs` (identity, contract gating, mask parsing, refusal
  behaviour) and the icon part by the new harness tests, so a separate file would have duplicated coverage inside a
  blind-written, unexecuted harness. Recorded here instead of adding an untested file to the release.
- **The local gates were extended, not loosened, and they caught real mistakes while writing this candidate.** The
  icon rule validates every ICO frame (count, sizes, bit depth, offsets inside the file); the platform rule fails any
  document line that names macOS or Linux without stating that the platform is not supported; both gates require the
  EXE-icon flags. `tools/api-check.py` was run after each edit and every complaint was fixed by renaming or registering
  code — never by editing the check.
- **`scripts/test.ps1` globs `tests\UnitTests\*.cs`**, so the four new dev4 suites (browser catalog, profiles, backup,
  metrics) are picked up by the existing test step with no script change. `scripts/test-windows.ps1` and
  `Xtension.csproj` keep explicit lists, and both were updated for every new file.
- **Environment limit, restated because it decides what "done" means here: nothing was compiled or executed.** No C#
  toolchain, no Windows desktop and no audio device exist in the authoring environment. `tools/verify-source.py`,
  `tools/api-check.py` and a tree-sitter C# parse (no syntax errors) are the only evidence available locally; the
  compile, the unit suites, the harness, the packaged binaries, the resource comparison and the real-Brave smoke are
  evidence that only the GitHub runners can produce. This entry is therefore a record of work, not of verification.

Verification order for this candidate, recorded before the fact: commit the work on the session branch, open the pull
request, wait for the required jobs, merge only when they are green, verify the merged commit, then create and push the
`v0.1.0-dev4` tag and confirm the release workflow published four assets plus `SHA256SUMS.txt` as a prerelease that is
not marked latest. The run IDs belong in the pull request and in the release notes, and any further entry here is
appended, never edited into this one.


## Entry 018 - 2026-10-16 - the verification round: what running it changed

The first CI runs compiled the candidate, ran the unit suites, ran the Windows harness, and then failed in ways that
source inspection had not shown. Those failures are recorded as findings 22-25 in `docs/DEV4_AUDIT.md`; this entry
records the decisions behind the fixes, because two of them are design decisions and not bug fixes.

- **One container per logical radio group, and the row wraps.** Windows Forms radio buttons are mutually exclusive by
  their parent container, so putting the visibility mode, the colour and the profile options in one group meant that
  choosing a colour unchecked the mode - silently, in the window and in the applied settings. The fix that makes the
  rule structural rather than remembered is one `FlowLayoutPanel` per logical group. The first attempt used
  `WrapContents = false`, and the harness then measured an option 348 px wide inside a 323 px column at 150% text in the
  smallest supported window: wrapping is what moves an option to a second line, and it is the reason the rows can
  survive both the narrow window and the large font.
- **Wrapping cannot fix a control that is wider than its column, so the labels themselves were shortened.** The mode
  pair, the profile names and the browser support tags became short names, and what the parentheses explained moved
  into the help paragraphs - which are `WrappingLabel`s that own their own height and already re-measure on a font or
  width change. The choice belongs on the control; the explanation belongs in the text that can wrap. The same strings
  are what the diagnostics panel prints for the active profile, so the two views now agree instead of the diagnostics
  showing "Balanced (default, same as before)".
- **The settings harness now checks the pairing that broke.** It selects the mode, then the colour, then requires the
  mode to be unchanged in the window and in the settings handed to the host. A regression that reintroduces a shared
  container fails on the spot instead of producing "the mode silently became always" months later.
- **A notification window is not made topmost in its constructor.** `TopMost` is applied by the window manager when a
  window is shown, and bringing a topmost window forward is exactly what a non-activating card must not do. Topmost
  now comes from the same `SetWindowPos` with `SWP_NOACTIVATE | SWP_SHOWWINDOW` placement the overlay uses, and the
  notice records a warning if it ever does end up holding the foreground rather than assuming it cannot.
- **An assertion about a log line uses the product's own formatter.** The Brave acceptance run looked for
  `browsers=brave` while the startup line writes `browsers brave`; the expected token now comes from
  `BrowserCatalog.MaskTokenList`, so the assertion fails when the shipped default changes and cannot fail on wording.
  A duplicated format string in a test is a defect waiting for its first execution.
- **The harness reports where its time went instead of just being slow.** The UI Automation test failed its 15 s
  deadline once on a loaded 32-bit runner and the message could say nothing about which step was slow. Each phase now
  records its own duration and a timeout prints them, so the next occurrence says whether the product's scan or the
  operating system's UI Automation plumbing underneath it was the slow part. The deadline itself is unchanged: nothing
  was loosened to make a job green.
- **Amended rule for any future gate work:** when a gate fails, read it as a statement about the repository first -
  two of the three failures in this round were genuine pre-existing defects in the published dev3 window, and one was
  a genuine defect in a test written for this candidate.

## Entry 019 — dev5 mandate and verified baseline (2026-10-18)

Owner scope: “Xtension v0.1.0-dev5 — Comprehensive Repository Audit, Reliability Engineering, Multi-Browser Support, Cross-Platform Feasibility and Verified Prerelease”. Priority order, verbatim:

> 1. **A — Comprehensive repository audit.** Identify existing defects, latent risks, incomplete implementations, inconsistent behavior, resource waste, and weaknesses in the development or release process. Investigate beyond the reported issues.
> 2. **C — Address unverified and residual risks.** Prioritize demonstrable reliability, safety, compatibility, lifecycle, UI Automation, concurrency, and real-browser problems according to evidence and severity.
> 3. **H — Add valuable new features.** Research, rank, and implement justified features after the repository audit and critical risk work. Do not add features merely to increase the feature count.

“retain the current design and fix defects only”; C# 5, .NET Framework 4.8, Windows x86/x64, fail-closed closing, settings compatibility, bilingual strings, MIT license and append-only history remain requirements. The owner reports the historical audio symptom is no longer encountered; this is not proof of its cause. No audio subsystem change is planned. Additional browsers require actual acceptance, not ancestry. macOS, Linux X11 and Wayland remain unsupported unless real implementations and verification exist. Normal PR merge and prerelease publication are authorized only after the acceptance gates; no manual owner session is mandatory.

Reconnaissance before implementation: clean session branch `arena/f7f71a18-xtension`; HEAD and fetched `origin/main` both `685975ec088ac28a722e004d6354c9fc1cbd6e63`. PRs 1–5 are merged, none open. Tags/releases dev1–dev4 exist; dev5 tag absent. Main CI `38924457492` succeeded. Published dev4 tag resolves to `15900f524951b14ec7658e2ddadf07166f10ed3a`; main adds the historical release record only. Release `38922772057` succeeded, dev4 prerelease=true/draft=false, four assets. Direct local asset download was attempted and failed at the disallowed release-asset host (EOF); Windows CI must download and hash the published executable before measuring it. Local Linux has no PowerShell, C# compiler or Windows desktop. Source inspection and Python aids are not execution evidence.

Initial safety finding: `TabInfo` has no runtime identity; `OnOverlayClicked` clears the displayed confirmation and `CloseGuard` compares only the newly selected tab's rectangle against the click. A different tab selected into the same rectangle can therefore satisfy the gate. Plan: carry the displayed tab's UIA runtime identity and geometry into the request, require an exact match in the fresh scan, and refuse missing identity. This strengthens rather than replaces all existing gates. Regression coverage must include a different identity at identical geometry.

Browser investigation order: Chrome and Edge first (existing catalog boundary, runner installations and independent real UIA acceptance possible); Firefox remains refused unless its different provider can be implemented and tested. Default mask remains Brave only. Support promotion is gated on actual CI evidence. Feature shortlist and platform analysis follow the full source trace in the audit. VERSION will change on this development branch before candidate CI, not by pushing a version edit directly to main; the merged, checked commit can then be tagged without an untested post-merge edit.

## Entry 020 — dev5 first implementation decisions (2026-10-18)

Source trace confirmed D5-01–08 in the new audit. Exact identity, transactional settings, settings ownership, callback marshalling, hook filtering and non-skipping latency assertions implemented before feature expansion. Chromium contract now also requires direct UIA Tab → TabItem parentage. Chrome/Edge are experimental candidate entries so CI can test actual enabled application behavior, not promoted to verified on ancestry. Six browser/architecture jobs must pass before publication. Existing Brave-only default stays unchanged. Both binaries retain all close deadlines/budgets/freshness/cooldown/modifier/foreground/geometry gates.

Candidate features ranked: (1) independently tested Chrome/Edge opt-in support (high workflow value, existing boundary, moderate maintenance, actual-run acceptance); (2) complete forward-compatible settings backups (unknown values currently survive rewrites/import but are omitted on export; bounded core tests possible); (3) keyboard/global hotkeys (deferred: activation conflicts and input complexity with little evidence of need); (4) a cross-platform rewrite (deferred pending separate native feasibility and acceptance). Current UI design is retained. Initial candidate CI will establish compiler/runtime facts before support promotion or any release decision.

## Entry 021 — dev5 continued audit and feature implementation (2026-10-18)

First CI `39106016202` executed static checks and core tests: 97 passed, 1 failed. The failure is a stale fixture that uses `browsers=chrome` as an invalid value; Chrome is now deliberately implemented for candidate acceptance. Changed the invalid-provider fixture to Firefox, retaining both rejection assertions. No successful Windows builds or browser runs occurred in that run (dependent jobs skipped).

Further click-lifecycle trace: a held mouse press could survive an overlay move or identity change. Cancel arming on real placement change and displayed-identity change, including identical geometry; capture the identity actually displayed, not the latest diagnostics result. Add synthetic mouse-down/target-change/mouse-up coverage. UIA runtime IDs are session-lifetime identities only, never stored or logged.

Selected feature #2 implemented: forward-compatible export now includes bounded unknown settings values as well as known typed values, while excluding backup metadata overrides. Full export/parse/apply round-trip test added. Existing key=value schema is retained (the earlier README's JSON wording was inaccurate); no dependency or UI redesign. Custom unknown values are configuration, not a sanitization boundary; users should inspect backups before sharing.

Performance protocol: three 20-second observations per binary/state, alternating order, fresh disposable browser profile per observation, same runner/browser/default configuration. Pin the previously recorded dev4 hash independently of its downloaded manifest and verify baseline tag/source VERSION and binary version. Report raw values and variability; do not invent a relative threshold from one runner's three samples. Existing readiness and acceptance latency ceilings remain gates. Safe real-browser stress adds 24 rapid selection switches with exact before/after tab-set preservation and overlay convergence.

## Entry 022 — lifecycle, UI and release governance (2026-10-18)

Further audit found the tab watcher retained its registered HWND after unsubscription; returning to the same browser HWND therefore skipped registration. Reset the identity on unsubscribe and add a real synthetic UIA registration-cycle regression. Found the settings import result erased by its own refresh; refresh first and set the outcome last, testing the same method called by the file dialog. No visual redesign.

Release governance strengthened in workflow: require version-matching prerelease tag at current merged main with successful exact-commit main push CI; real-browser jobs now run exact release-build artifacts before publish (three browsers, both architectures). This is additional gating, not permission to skip candidate CI. Platform feasibility report documents separate native-host paths and permission/identity/input boundaries. Windows host remains; macOS and Linux X11/Wayland are unsupported. No native GUI environment exists to verify a port and none is attempted.

## Entry 023 — diagnostics evidence and bounded fixture transport (2026-10-18)

Audit found the dev4 lane test could not prove separation: it checked enum inequality and a single sample, while only aggregate percentiles were exposed. Added per-lane retained sample counts and scan/queue p95 to the existing diagnostics text, with mixed-duration regression. No safety input reads metrics. Bilingual backup messages now match actual default-on-rejected-value behavior and disclose user-authored unknown values. Current documentation corrected JSON/tray-option claims without redesigning the UI.

Candidate execution transport is bounded independently of browser descendant lifetime: file-backed stdout/stderr, exact exit-code propagation, timeout failure and filtered evidence. A script regression starts a parent with a surviving descendant, verifies parent code/output while the child is still alive, and requires deadline failure for a stalled process. This is a harness reliability change, not a claim about the cause of a browser stall; the outstanding run must provide its own actual result.

## Entry 024 — scope rule clarification (2026-10-18)

The owner explicitly approved multi-browser expansion. AGENTS.md's Brave-only product/rule wording therefore needs the minimal generalization to “enabled supported browser”, with the new exact runtime-identity requirement added. The foreground requirement itself is not loosened: it must be the very window validated, an implemented provider, and user-enabled. Framework/compiler/platform constraints remain unchanged. This reconciles the contributor instructions with the approved product evolution, rather than leaving a rule that contradicts tested additional providers.

## Entry 025 — Edge contract diagnosis and lifecycle coverage (2026-10-18)

The file-backed runner now returns Chrome success for both architectures; Edge fails on both with post-restart ScanIncomplete (197 nodes, ~168/338 ms). This is not a speed/budget justification: no limit or parent/identity gate is to be loosened. Before changing any provider assumption, add privacy-safe first-incomplete-cause metadata (operation/enum/numbers only, never names) so the failed shape is identifiable. The release remains blocked. Additional Windows regressions exercise repeated context teardown and named-instance signalling; these are synthetic lifecycle checks, not Shell visual/startup acceptance.

## Entry 026 — symmetric event-to-visible comparison (2026-10-18)

The resource harness can measure a real foreground tab-switch-to-overlay-update interval for both EXEs, not just candidate hover latency. Add five Ctrl+Tab transitions after each 20-second foreground resource window, with the same scanner, UIA identities, geometry, fixture and foreground checks for both binaries. Time native input submission to observed changed overlay bounds, then verify the resulting exact selected-tab geometry. Missing/failed samples are explicit, not zero; this is coarse harness observation, not a physical display latency or close authorization. Keep resource CPU/memory sampling separate and do not alter product safety timers or introduce performance acceptance thresholds. Collect n/p50/p95/max and disclose the small sample and shared-runner noise.

## Entry 027 — evidence-backed browser scope decision (2026-10-18)

Run 39108277770 reproduces Edge failure in both architectures and identifies DepthBudget, not time (197 nodes, 160/181 ms). There is no recorded, stable narrower Edge chrome-root contract that would justify skipping the deeper subtree. Do not increase the cap, ignore incomplete nodes, disable built-in UI merely for the fixture, or claim another browser because Chromium is shared. Withdraw Edge from offerable/implemented providers for dev5; keep exact process detection for diagnostics and add explicit actual-EXE refusal tests for a signed isolated Edge fixture in both architectures. These are named refusal tests, not replacement green acceptance. Retain the failed acceptance evidence and rank a bounded Edge-specific root contract as follow-up. Chrome independently passed full acceptance in both architectures on 5c97764 / 39108277770, so it can carry that evidence; exact-head gates continue to retest it. The preference for two additional browsers is not fully delivered; safe verified expansion is Brave + opt-in Chrome, not a knowingly incomplete Edge claim.

## Entry 028 — explicit pure-core process architectures (2026-10-18)

The successful pure-core job ran twice under two PowerShell hosts, but test.ps1 emitted default AnyCPU, not separately targeted x86/x64. Do not confuse host parity with process-architecture evidence. Give the existing test script an explicit x86/x64 parameter and distinct output names; run both architectures under both hosts in CI, and use the release build's matrix architecture. No product, compiler language/framework or runtime dependency changes. Correct the audit wording until the new runs prove it.

## Entry 029 — least-privilege release-gate dependency (2026-10-18)

PR #6 was normally merged at e10de7969531c78a2260b74954bf1190794aee8b after exact-head CI 39109650977 passed all 12 jobs. Before creating an immutable tag, explicitly declare `actions: read` on verify-tag, the only release job that queries workflow runs. The repository is public and this is not an observed permission failure; it makes the new API dependency explicit rather than relying on public-read/token behavior. Preserve contents:read and no write permission on that job. Submit this narrowly scoped workflow/log follow-up through another normal PR and complete exact-head/main gates; no direct main push, permission bypass or tag replacement.
