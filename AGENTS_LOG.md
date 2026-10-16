# AGENTS_LOG.md — append-only record of agent work

Rules: append new dated entries at the bottom. Do not rewrite or delete earlier entries. Never record tokens, credentials, or private tab or window titles.

## 2026-10-14 — Release preparation for v0.1.0-dev1

- Audited the uploaded source zip. Found: hard-coded version literals in several files; fixed-pixel overlay thresholds that fail at 200% DPI; a synchronous UI-thread UI Automation scan every 650 ms (a hung Brave froze the tray); a close path that did not re-check foreground at click time; a close check that compared overlay bounds with expected bounds, so valid clicks could look stale; tab titles written to the log; and a repeated `Process.GetProcessById` call on every check.
- Rewrote the source as focused files: `src/Core`, `src/Interop`, `src/Uia`, `src/App`, `src/UI`. Introduced a fail-closed `CloseGuard`, an epoch-based state machine, a dedicated scan thread with a single pending slot, a 5 s stall detector, and a non-activating overlay.
- Added single-version generation from `VERSION`, an explicit csproj compile list, build/test/verify/package scripts, unit tests for the pure core, and CI and tag-release workflows.
- Verification performed in this environment: no C# compiler and no PowerShell were available. Checks run: tree-sitter C# parser over all 31 source and test files (syntax only, no errors); a cross-reference script for `Class.Member` references among project types (no unresolved references, after a false positive for generic `Assert.AreEqual<T>`). Not run: csc, msbuild, PowerShell scripts, unit tests, GUI, Brave.
- Removed `Xtension-v0.1.0-source.zip` from the working tree. The zip is recoverable from commit `60326da`. The release source ZIP is now built from git by `scripts/package.ps1`.
- Left LICENSE unchanged.

## 2026-10-14 — CI run 1 failed in "Verify source consistency"; fixed

- Run 38566288405 (PR #1) failed at the verify step after about 14 s. The job log could not be downloaded from the sandbox (blob fetch returned EOF), so the cause was found by code review, not from the log.
- Defects found and fixed in `scripts/verify-source.ps1`: a trailing comma in an array literal (a PowerShell parse error); a wrong modifier-name pattern (`VK_SHIFT` where the code uses `VkShift`); a case-insensitive `-match` that matched the word "titles" in a comment in `AppLog.cs`. C# checks now use `-cmatch` and run on code with comments removed.
- Also changed `scripts/package.ps1` to pass git arguments as an array and to avoid `1MB` literals in the verify script. Both are plain PowerShell.
- Checks run after the fix: all six `scripts/*.ps1` files parse with a PowerShell tree-sitter grammar (syntax only; PowerShell itself is not installable here); a Python mirror of the verify rules passes. Not run: PowerShell execution, csc, unit tests, GUI, Brave.
- Lesson recorded for future agents: a parse check and a rule mirror are not a substitute for running the real script. Read CI logs with `gh run view --job <id> --log` and keep the run ID in the next entry.

## 2026-10-14 — CI run 2 failed at the same step; second cause fixed

- Run 38566665861 failed in the same step. Cause (by code review): the verify script looked for `SHA256SUMS` in `release.yml`, but the name is only in `scripts/package.ps1`. The Python mirror had checked the right file, so it did not reproduce the mismatch.
- Fix: the check now reads `scripts/package.ps1`. `release.yml` also shows the checksum file in a step, so the release log includes it.
- Still unverified until a CI run completes: PowerShell execution, csc builds, and unit tests.

## 2026-10-14 — CI run 3: unit-test step failed; diagnostics added

- Run 38566872550: static checks now pass; "Run unit tests (pure core)" failed. The job log is not downloadable from the sandbox (the log host is outside the allowed hosts), but check-run annotations are available through api.github.com.
- Scripts now emit `::error` annotations for failed source checks, csc errors, and failing tests, so the failure text can be read through the API.

## 2026-10-14 — CI run 4: static checks, unit tests, MSBuild check passed

- Run 38567945567: "Static checks and unit tests" passed (source checks, csc compile of the core and tests, unit tests run). "MSBuild project check" passed. The x86 and x64 builds failed on csc flag `/deterministic`, which the .NET Framework 4.8 csc does not accept. Removed that flag.
- Earlier failures in this series were fixed with `/debug:pdbonly` (csc rejects `portable`) and by removing a read of `$LASTEXITCODE` before any native command.
- Source of these results: check-run annotations via api.github.com (CI logs are not downloadable from the sandbox).

## 2026-10-14 — CI run 5 and release run: green; corrections to earlier notes

- Correction: earlier notes said `src/Program.cs` was missing. It is present and tracked (entry point with single-instance handling, DPI setup, and exception handlers). The build and MSBuild jobs compile it.
- Run 38568246970 (head `1a6017d`): the x86 and x64 builds failed on `WindowsBase.dll` not found. It lives under `Framework64\v4.0.30319\WPF\`, and the simple-name fallback does not work for csc. Fixed in `8774575`: each reference is resolved from `WPF\` first, then the framework folder, and the script fails with a clear message if one is missing.
- Run 38568637454 (head `8774575`, PR #1): all four jobs passed: static checks and unit tests, MSBuild project check, build x86, build x64. Each build uploaded its EXE artifact. Source of results: check-run status and conclusions via api.github.com.
- Tag `v0.1.0-dev1` created as an annotated tag on `8774575`, the CI-verified commit, and pushed. Tags were not moved or deleted.
- Run 38568996101 (tag push, release workflow): verify tag, build x86, build x64, and publish prerelease all succeeded.
- Published release: title `Xtension v0.1.0-dev1`, prerelease, not draft, no release marked latest (the latest-release API returns 404). Assets: `Xtension-v0.1.0-dev1-win-x86.exe` (67584 bytes), `Xtension-v0.1.0-dev1-win-x64.exe` (67072 bytes), `Xtension-v0.1.0-dev1-source.zip` (72537 bytes), `SHA256SUMS.txt` (296 bytes).
- Not verified: downloading `SHA256SUMS.txt` back from the release failed with an EOF from the release-asset host, which is outside the allowed hosts. The manifest content was not read here. The workflow self-verifies the checksums before publishing.
- Not verified: anything at runtime. The EXEs were compiled in CI but not run. No GUI or Brave behavior has been tested. No performance numbers have been measured.
- Open: the six earlier Vietnamese quotes (see DESIGN_LOG entry 006) are still missing. PR #1 is open and has not been merged.

## 2026-10-14 — dev2 reconnaissance and implementation (checks pending)

- Read AGENTS.md, both append-only logs, README, changelog, capabilities, VERSION/project/manifest, all application and test sources, scripts and both workflows before implementation. Clean baseline on the session branch from `804a256`; remote default main; PR #1 merged; dev1 prerelease exists; dev2 does not exist at reconnaissance. Local shallow checkout has no tags, so remote heads/tags/releases were queried explicitly.
- Baseline evidence: CI run 38570045403 on main succeeded (source checks, pure-core tests, x86/x64 builds, MSBuild). Prior dev1 release run 38568996101 succeeded. Old GUI claims remain unverified. Local environment is Linux x86_64 with no PowerShell, .NET/Mono/MSBuild or Windows desktop; no installed/accessible Brave-on-Windows session. No local Windows commands or Brave acceptance can truthfully be reported as run.
- Traced the supplied generic UIA failure through scanner, worker, controller and Diagnostics. The source uses “UI Automation”, not “UI animation”. Concrete cache-overload misuse found; actual user exception/HRESULT/stack unavailable. Inspected dotnet/wpf implementation through GitHub API. Added explicit navigation cache and safe staged error metadata; regression fixture pending Windows CI.
- Implemented core freshness/selection/geometry corrections, controller result-age/foreground/geometry/epoch checks, PID-aware identity and uncached close validation, checked native overlay visibility, measured/scrollable dashboard and diagnostics, error-signature refresh, wait-handle and tray resource cleanup, fail-closed mutex failure, and 32-bit build-tool discovery. Added pure-core and Windows synthetic regressions. See forthcoming audit report for evidence, severity and unresolved risks.
- VERSION is now the requested candidate. Derived artifacts remain generated by update-version.ps1 in Windows build/test paths; none are manually authored or tracked. CI adds Windows PowerShell 5.1 synthetic UIA/layout checks in both architectures. Tests/builds/CI results are pending, not presumed green. No tag, new release or merge performed.

## 2026-10-14 — First candidate CI exposed layout defect and fixture startup error

- PR #2 opened, unmerged: https://github.com/coderunknow/Xtension/pull/2. First implementation commit `841b8bd` pushed only to the session branch.
- CI run 38573791546: source verification, core tests, x86 and x64 compilation, and MSBuild passed. Both new Windows regression jobs failed: dashboard action bounds at 200% scaled text / narrow size, and WaitForInputIdle on the console-hosted fixture process. Overlay show/hide and changed-error diagnostics tests passed (2 of 4 Windows tests per architecture).
- Corrected the wrap-constrained startup checkbox, retaining the layout assertion and adding bounds details. Replaced the invalid console-process WaitForInputIdle assumption with an explicit named-event Form.Shown handshake; no UIA assertion skipped.
- Added exact-HEAD CI checkout and packaging provenance stamps (commit/version/architecture/clean-tree/hash plus binary product version and PE machine checks). CI packaging tests reject five invalid provenance cases and verify checksums. Release workflow retains tag-only prerelease/not-latest behavior and carries build stamps between jobs. Pending next run, not claimed verified yet.

## 2026-10-14 — Cache defect reproduced on Windows; broader verification attempt

- CI run 38574220910 on exact candidate `037ee19922e07007cd8d92d7bfa4d69ad32e3f70` passed all five jobs: source/core, x86/x64 builds plus four Windows tests each, MSBuild, packaging/provenance/checksums. Windows annotations from both architectures report legacy `Cached.ControlType: InvalidOperationException (HRESULT 0x80131509)`, then successful explicit-cache scanner confirmation of the fixture's second tab. This is observed Windows evidence, not the user's unknown exception.
- The 200% font/narrow-window action clipping assertion now passes after constrained checkbox wrapping. Physical monitor scaling is still distinct and unverified.
- Added worker busy/error/recovery/disposal and controller epoch/dashboard isolation tests. Retain safe diagnostics from current-epoch late results without authorizing them; suppress overlay while a worker is stalled; remove unused window-title collection code.
- Since Windows CI can display synthetic windows, a real-Brave attempt is justified rather than assuming no Windows testing is possible: added an isolated profile smoke harness that starts the actual built EXE against a checksum-verified official Brave v1.97.56 portable x64 download. It tests initial recognition, actual overlay, tab switching, modifier clicks, one real mouse close and minimize/restore. No personal profile is used; no title/URL/provider output is forwarded. This attempted check is pending, may expose acceptance blockers, and is not a full real-desktop matrix.

## 2026-10-14 — Actual EXE / Brave smoke passed; review services unavailable

- CI run 38575016732 on `f3022bb` passed all six jobs, including 38 core tests, six Windows regressions per architecture and actual x64 candidate EXE against official Brave v1.97.56 (`ProductVersion=155.1.97.56`) on Windows runner OS 10.0.26100.0, DPI 96. Annotations confirm 64 UIA nodes, selected-tab overlay, three tab switches, Shift/Alt/Win clicks preserving three tabs, exactly one mouse-click tab closure, and minimize/restore recovery. This supersedes earlier statements that no real browser runtime was possible; local Linux remains unable to do so.
- Strengthening the final candidate test to compare selected/unselected UIA runtime identities before/after close, exercise multiple windows, focus away/return, move/resize/maximize, many tabs and long synthetic titles, and run actual EXEs of both architectures against isolated Brave. No screenshots, browser profiles, titles or URLs are uploaded. Fixture cleanup is restricted to executable paths created by the test and the script refuses non-Actions desktops.
- PR review services: Kilo check is ACTION_REQUIRED because the account is out of credits (bot comment), Qodo trial ended, Copilot quota exhausted, CodeRabbit skipped review. These are not code findings or successful independent reviews. Main branch metadata reports protected=false and empty required-check contexts; ruleset list is empty. Administrative required-status-check endpoint returned HTTP 403 Resource not accessible by integration; no settings or checks were bypassed or changed. Publication must disclose these external review limitations.

## 2026-10-14 — Expanded real-Brave matrix green; final documentation gate

- Run 38575630606 on exact `e382a8aeef2dfc40df10247c138f0f318a859451` passed all seven jobs: source/core, x86/x64 builds and six Windows tests each, MSBuild, packaging/provenance, and real-Brave smoke in both process architectures. Real close now checks UIA runtime identities (selected removed, unselected retained), not merely tab counts. Two windows, focus changes, geometry transitions, many tabs and long synthetic titles also completed successfully.
- Updated CHANGELOG, README, RELEASE_CAPABILITIES and DEV2_AUDIT to distinguish actual Brave smoke from remaining manual acceptance gaps. Added Windows PowerShell 5.1 source/core parity and compact evidence annotations (GitHub limits annotations per step). Added post-publication asset download/hash verification to the existing tag-only release workflow.
- Local checks: git diff --check, exact src/csproj set equality, all PowerShell ASCII, original history prefixes unchanged. Local Windows execution remains unavailable; all Windows execution claims above are remote runner results.
- Candidate release is justified by established cache cause and both actual EXEs' successful primary behavior, conditional on final exact-commit CI. External review quotas remain a disclosed optional-review limitation. No tag/release/merge performed yet; final workflow IDs/outcome will be appended after observing them.

## 2026-10-14 — Prerelease publication completed, not merely started

- Final candidate `816d7664b79d8927731305c8f974533d65bae714`: CI 38576245878 success. Source/core ran under PowerShell 7 and Windows PowerShell 5.1, annotations explicitly report 38 passed/0 failed. Six Windows regressions passed per architecture; both real-Brave annotations include all twelve-tab/long-title and selected-runtime-identity checks.
- Created/pushed annotated v0.1.0-dev2 only after the clean-tree/version/CI/open-PR/absence gate. Release run 38576648574 succeeded, including post-publication downloads and hash verification. Release ID 412788346; prerelease=true, draft=false; latest-release API 404. Main remains `804a256c273e830a6e2e862e3775fbf5596354b6`; PR #2 remains open/unmerged.
- REST asset digests and successful publish-job annotations agree: x86 cfedbd004ce7cc5b68db937ea31c4a3115257aedf772e3a95a360f1cb66f3ce5; x64 25c836137928a274380fd63d2650499df06b92103c4ef30e834c907a5620dc9c; source 450cc1b214cdba0c72a32872c830dee305a72de0f7a0858ff8bd1f00d9604122. Manifest digest 20311a8959fab00832e8c754668b13c2167b514665dc48cce06159ee0e4d6e95; workflow compared downloaded manifest to packaged manifest and checked all entries.
- External Kilo check remains ACTION_REQUIRED (credits), not a repository CI failure or a completed independent review. No reviewer/protection settings were changed. Added optional published_tag workflow_dispatch input for an additional real-Brave run of the actual downloaded release EXEs; app source and existing release tag remain unchanged. Result pending at this entry.

## 2026-10-14 — Manual dispatch permission boundary; use normal PR CI for matching-release checks

- `gh workflow run ci.yml --ref arena/8f3060a9-xtension -f published_tag=v0.1.0-dev2` returned HTTP 403 `Resource not accessible by integration`. No manual dispatch was created; publication and its successful asset-integrity verification are unaffected. This is an optional Actions write-permission boundary, not an invented test result.
- Extended the already-authorized PR CI path to detect an existing release derived from VERSION. It substitutes published binaries only after an exact git diff of application source/project/manifest/version/build-generation inputs against the fetched release tag, and verifies the downloaded manifest hash for each EXE. New/unpublished or different-source candidates continue testing their newly built EXEs. Explicit manual requests fail if unavailable/mismatched. No repository protection or token permissions are changed; all operations use ordinary checkout/content reads and the existing branch push trigger.
- This follow-up modifies only CI/history; the published source/tag and application implementation remain unchanged. Actual downloaded-binary runtime result is still pending at this entry.

## 2026-10-14 — Post-publication rerun found an invalid multi-window fixture expectation

- Post-release CI 38577791624 (history/CI-only commit `48b3530`) passed source/core/build/Windows/package checks, but both Brave jobs failed at `overlay survived minimize`, after visibility, switches, multiple windows, focus transitions, modifiers and exact selected-identity close passed.
- The fixture left its second Brave window normal while minimizing the first, then required no overlay at all. That expectation is invalid if Windows foregrounds the remaining Brave window: correct application behavior is to show that window's confirmed-tab overlay. The failed run did not record the foreground HWND, so the precise focus ordering in that run is not claimed as directly observed.
- Corrected the setup, not the safety assertion: explicitly minimize the secondary browser window after its own acceptance check; before accepting primary minimize, require BOTH that primary is iconic and that the foreground is not Brave AND that no overlay exists. This is deterministic and preserves the intended hide-on-minimize scenario. Only the test harness changed; released application code/tag/assets are untouched. The corrected published-binary run is pending.

## 2026-10-14 — Exact published-byte primary checks passed; minimize predicate needs disambiguation

- CI 38578285359 downloaded each actual release EXE and recorded hashes exactly matching the published assets. Both architectures passed primary recognition/placement, tab switches, two-window/focus transitions, modifier rejection and selected-runtime-identity close. Both then failed the combined minimize predicate; all other repository jobs passed. Superseded run 38578001021 was cancelled by normal PR concurrency, not reported as passed.
- The predicate combined native iconic state, no overlay and a non-Brave foreground process. It did not report which clause failed. A minimized Brave HWND remaining foreground is not itself an overlay defect: production explicitly checks IsIconic. Removed that unsupported foreground-process assumption, retained the requirement that BOTH browser windows are iconic and NO overlay is visible, and added privacy-safe HWND/iconic/visibility diagnostics on success/failure. No outcome from this revised predicate is presumed. Application code and published assets remain unchanged.

## 2026-10-14 — Final published-byte evidence and handoff

- Run 38579364444 succeeded on `175369bd07c174747503a80187cd12beddd79851`. All seven CI jobs passed. Jobs 115905008479 (x86) and 115905008568 (x64) explicitly recorded substitution of the published EXEs with hashes cfedbd004ce7cc5b68db937ea31c4a3115257aedf772e3a95a360f1cb66f3ce5 and 25c836137928a274380fd63d2650499df06b92103c4ef30e834c907a5620dc9c respectively, followed by every Brave smoke PASS. This is actual downloaded-release binary execution, not merely recompilation.
- Observed native minimize state in BOTH jobs: primaryIconic=True, secondaryIconic=True, foregroundIsPrimary=True, foregroundIconic=True, foregroundBrave=True, priorOverlayVisible=False, visibleSmallWindow=0. This establishes the previously unsupported test assumption. The two prior failures are retained above rather than hidden or reported as successes.
- Tag remains `v0.1.0-dev2` -> `816d7664b79d8927731305c8f974533d65bae714`; release remains published/prerelease/not-latest with four unchanged assets. Post-release commits modify only history, audit documentation, CI and test harness; git diff of application source/project/manifest/version/build-generation inputs against the tag is empty. Main remains the original `804a256` baseline. PR #2 is open/unmerged.
- Final handoff documentation records exact release source, candidate CI 38576245878, publication 38576648574, and downloaded-byte acceptance 38579364444. A subsequent documentation-only push is checked by the same PR CI; its live/latest result is linked by PR #2 rather than creating an endless self-referential log-commit cycle.
- Remaining items: detailed severity/impact/follow-up in docs/DEV2_AUDIT.md. Physical mixed-monitor/real DPI, native x86 OS, rapid physical-click/tab-switch race stress, unsupported layouts, true hung-provider recovery, full tray/startup/graceful shutdown and visual diacritics inspection are not claimed accepted. Kilo ACTION_REQUIRED is a credit-limited optional review, not green; Qodo/Copilot/CodeRabbit did not review. Manual dispatch was denied (403); equivalent automatic PR verification completed successfully. No merge, force push, tag replacement, private profile/screenshot/log, dependency addition or license change occurred.

## 2026-10-14 — Repeated final verification and minor follow-up disclosure

- CI 38580406511 passed all seven jobs, repeating the successful actual-published-EXE run 38579364444. The x86/x64 downloaded hashes remain identical and both minimize checks report iconic Brave foreground with the overlay correctly hidden. Optional third-party billing/quota checks remain explicitly separate.
- Final source review recorded one low-severity, source-inferred wording edge case in DEV2_AUDIT: if the OS retains a minimized non-Brave HWND as foreground, Tick's idle message may call it “Brave minimized” because the message condition does not include isBrave. No unsafe close or overlay authorization results; the non-Brave case was not reproduced. Recommended next-patch correction/regression is documented; existing immutable release is not altered for a minor wording edge case.
- PR and release notes now link successful exact-published-byte acceptance. The older local `gh pr edit` encountered a GraphQL Projects-classic deprecation error; equivalent PR body update through `gh api PATCH` succeeded. Release-note supplement also succeeded through the Releases API; no tag, asset, publication flag or merge state was changed.

## 2026-10-14 — Owner-requested merge preparation

- User explicitly requested: “merge and complete the final things.” This supersedes earlier no-merge instructions; historical entries are preserved.
- Reconfirmed PR #2 open, mergeable, targeting main, head `107d3be0829ecc90648daf7d3fcdc549520000a3`; repository CI 38580888628 passed all seven jobs. Optional Kilo review remains ACTION_REQUIRED from the previously documented credit limit; it is not represented as a completed review or bypassed with admin privileges.
- The resumed workspace had baseline Git metadata with the completed work restored as files. Fetched the existing remote session branch, verified every working file's Git-normalized blob exactly matches the published PR head, and restored only branch/index metadata with soft/mixed reset. No working file was overwritten, user change discarded, branch switched, or remote history rewritten.
- Confirmed dev2 remains a published prerelease with four unchanged asset digests. Added this authorization record and clarified the audit's historical open-PR language; no application, test, version, packaging or release-workflow behavior changes.
- Next gates: CI on this documentation commit, explicit normal merge of PR #2 with a matching-head guard, then observed main CI. The final merge SHA and main CI outcome will be recorded on PR #2 and in the handoff, not claimed in advance here. All existing acceptance gaps remain documented.

## 2026-10-15 — dev3 reconnaissance and checkpoint 1

- Baseline verified before any edit: `git status` clean on `arena/8c38d759-xtension` (branched from `main` tip `5917ded`, the merge commit of PR #2); remote heads fetched; tags `v0.1.0-dev1` and `v0.1.0-dev2` present; `gh release list` shows both as prereleases; no open PR; `VERSION` was `0.1.0-dev2`. Dev2's published assets were not touched.
- Read before editing: `AGENTS.md`, both append-only logs, `CHANGELOG.md`, `README.md`, `VERSION`, `docs/DEV2_AUDIT.md`, `docs/RELEASE_CAPABILITIES.md`, `Xtension.csproj`, `app.manifest.template`, all nine PowerShell scripts, both workflows, all 26 source files and all 8 test files. No dev2 correction was reverted.
- Local environment: Linux sandbox, no csc/mono/msbuild/PowerShell, so nothing here can compile or run the tests. Available local gates used for this session: a tree-sitter C# syntax parse of every `.cs` file (0 errors on the untouched tree) and a Python mirror of `scripts/verify-source.ps1` plus the new localisation/discipline rules. All real compilation, unit tests, synthetic Windows tests and real-Brave runs happen only in GitHub Windows CI and will be reported from actual results.
- Audit findings carried into this session (details and severities in `docs/DEV3_AUDIT.md`): `SettingsStore` re-reads and re-parses the file on every getter call, loads an unbounded file into memory, drops unknown keys on rewrite and can leave `settings.ini.tmp` behind after a failed replacement; `CloseButtonOverlay.ShowAt` performs `SetWindowPos` + `GetWindowBounds` + `Invalidate` on every scan even when bounds are unchanged; `ProcessIdentity` never caches negative verdicts, so a non-Brave foreground re-opens a process handle every tick; `UiaTabScanner` never disposes the per-scan `CacheRequest`; the controller allocates a snapshot and pushes a render on every timer tick even when nothing observable changed; the dev2 "Brave minimized" wording edge case (message chosen from `minimized` without testing `isBrave`) is still present; there is no typed settings API, no Settings window, no visibility mode and no configurable button size.
- Implemented so far in this checkpoint: design-log entry 013 (request, decisions, trade-offs) and this work-log entry. Next concrete action: write the Core layer (`Strings`, `AppSettings`, hardened `SettingsStore`, parameterised `TabGeometry`, `HoverPolicy`) and pass the local syntax gate.

## 2026-10-15 — dev3 implementation checkpoint 2

- Implemented on `arena/8c38d759-xtension` (no commit, push, tag or PR yet at the time of writing): typed settings with a cached, size-bounded, unknown-key-preserving store; parameterised overlay geometry (18-40 logical px, inset 0-12); hover visibility mode with a bounded keep-time and display-only gating; a settings window behind an `ISettingsHost` contract; a Vietnamese/English string table with an immediate language switch; pointer reading without pointer injection; and the measured cost fixes (idempotent placement, negative identity caching, change-gated notification, `CacheRequest` disposal, hover scan gating).
- Regression coverage written in the same change: core tests for settings persistence/clamping/corruption/reset, parameterised geometry, the hover state machine (including the grace boundary and a clock that goes backwards), string-table completeness in both languages; Windows synthetic tests for overlay resize and idempotent placement, settings-window layout at 100/125/150/200% text scaling, in-place language switching, host round trip with out-of-range values, and a real Ctrl+W chord that must be delivered in full and leave no key down; a real-Brave second phase for hover mode, the configured 40 px button, a click on a hover-shown button, persistence across a restart with Brave open and recovery from a corrupted settings file.
- Local gates available in this environment, all green before the push: tree-sitter C# parse of every file (0 errors), the identifier/string-table cross-check (`tools/api-check.py`), the static-rule mirror (`tools/verify-source.py`, including the new rules that Vietnamese text may only appear in the string table and that `NativeMethods` may not move the pointer), and YAML validation of both workflows. These prove syntax, identifier consistency and static rules only. They do not compile the project, and no test or browser run was performed here.
- Release plumbing added: `VERSION` = `0.1.0-dev3`, the CHANGELOG dev3 section, README settings/hover/language tables, `docs/DEV3_AUDIT.md`, an updated capability boundary, the `performance` CI job and `scripts/measure-performance.ps1` (published dev2 EXE versus candidate, same runner). Next: push, open the PR against `main` (open/unmerged), read the CI result from the run, and only then ask the owner to test the CI-built EXE.

## 2026-10-15 — dev3 correction: the CacheRequest finding was wrong

- CI run 38727272108 compiled the whole application and the Windows harness and failed on a single call:
  `src/Uia/UiaTabScanner.cs` called `state.Cache.Dispose()`. In .NET Framework 4.8 `System.Windows.Automation.CacheRequest`
  has no `Dispose`; the disposable object is the `CacheRequestActivation` returned by `Activate()`, which the existing
  `using` block already releases. The "per-scan CacheRequest leak" recorded as finding 8 was therefore an incorrect
  hypothesis, and the attempted fix was itself the only defect it introduced.
- Corrected rather than hidden: the call is removed and replaced with a comment recording the verified API surface, the
  CHANGELOG entry now says the finding was refuted instead of fixed, `docs/DEV3_AUDIT.md` finding 8 now documents the
  investigation and its withdrawal, and the performance section lists the item under "investigated and not changed".
  The earlier entry in this log that describes the fix stays as written; this entry supersedes its conclusion.
- Lesson recorded for later work: this environment has no compiler, and identifier checks cannot know whether a
  framework type exposes a member. An API-surface claim about a BCL type is only settled by the compiler, so it must be
  stated as a hypothesis until CI has compiled it.

## 2026-10-15 — dev3 correction: the Windows harness did not compile

- CI run 38727819083 (`56c7b4c`) fixed the application build: "Static checks and unit tests" and "MSBuild project check"
  both succeeded, and the check-run annotations for the two `Build` jobs named five errors, all inside the Windows test
  harness and all mine rather than the product's:
  `tests\WindowsTests\Program.cs` used a control list that the method never declared (CS0103, twice), `Measurement` was unknown
  because `scripts/test-windows.ps1` compiles an explicit file list that never listed `tests\WindowsTests\Measurement.cs`
  (CS0103), and `tests\WindowsTests\BraveAcceptance.cs` declared a nested `window` inside a scope that already used an
  outer `window` (CS0136, twice).
- Fixes: the settings test now declares its control list where it starts using it; `Measurement.cs` is in the harness
  file list; the browser-bounds local is named `braveBounds` so it no longer collides with the overlay-window local.
  No product file changed in this commit.
- Cause worth remembering: the harness file list is hand-maintained while `Xtension.csproj` and the application build
  glob the tree, so a new test file can be compiled by nothing and still be "missing" at the same time. A guard for
  that gap is being added to the local and CI check sets.

## 2026-10-15 - dev3 CI: builds and measurement green, two harness defects fixed

- CI run 38728609891 (`120a5de`): "Static checks and unit tests", "MSBuild project check", "Build x86", "Build x64" and
  "Package provenance and checksums" all succeeded, and the new "Published baseline vs candidate resources (x64)" job
  succeeded as well, so both dev3 EXEs and the published dev2 baseline were measured on the same runner. The only red
  jobs were the two "Isolated real Brave smoke" jobs, and they failed in step 4, "Detect matching published application
  source", before any browser work started.
- Cause: that step ran `& gh release view <tag> --json tagName 2>$null` while `$ErrorActionPreference` was `'Stop'`. On
  Windows PowerShell 5.1 a redirected native stderr line is promoted to a terminating error and `2>$null` does not help,
  so the "release not found" message that `gh` writes for the still-unpublished `v0.1.0-dev3` killed the step instead of
  the intended "no matching publication yet" fallback. The branch had never run before: it was added while `v0.1.0-dev2`
  already existed. The probe now saves the preference, probes with `'Continue'` and decides from `$LASTEXITCODE`, and the
  hazard is written down in `AGENTS.md` rule 5.
- Also in this batch (`120a5de`), the Windows harness itself was made to compile: `scripts/test-windows.ps1` now lists
  `tests\WindowsTests\Measurement.cs`, `SettingsWindowReflectsHostAndClampsEveryValue` declares the control list it
  uses, and the real-Brave phase names the browser bounds `braveBounds` instead of shadowing `window`. The synthetic
  harness then reported "10 passed, 0 failed" on the runner.
- Not yet verified: the real-Brave acceptance phase for dev3. It only starts once the publication probe no longer aborts
  the job, so its result is the next thing to read from CI.

## 2026-10-15 - dev3 guards: harness file lists, local scopes and readable measurements

- The dev3 change set lost four CI cycles to mistakes a compiler would have caught in one pass, two of which were
  structural rather than logical: a test file that no harness compiled (`Measurement.cs`) and a local that shadowed a
  name in an enclosing scope (`window` in the real-Brave phase). Both classes now have a gate that runs before the push.
- `scripts/verify-source.ps1` and its local mirror `tools/verify-source.py` gained two rules: every file under
  `tests/WindowsTests` must be named by `scripts/test-windows.ps1` (the harness compiles a hand-written list while
  `src/` and `tests/UnitTests` glob), and every test file a script names must exist. Both directions are proven by fault
  injection: removing the `Measurement.cs` entry, and pointing a script at a non-existent test file, each make the check
  fail with the file named.
- `tools/api-check.py` gained a local-scope audit: it attributes every local declaration to its block chain and reports a
  name declared twice in one chain - the compiler's CS0136 (a declaration inside a scope that already declared the name)
  and CS0128 (the same scope twice). Sibling blocks, two methods and a local that repeats a field name are legal and are
  not reported; declarations inside `for`/`foreach`/`using` headers are not attributed to a scope at all, so a shadow
  that involves one is not reported. Proven by fault injection: restoring the pre-fix `window` shadow in
  `tests/WindowsTests/BraveAcceptance.cs` reproduces exactly the two CI annotations (lines 324 and 347 against line 264,
  CS0136) and nothing else. It cannot see a name that nothing declares (CS0103), so it does not replace a compiler; the
  tree is currently clean at 497 local declarations.
- `scripts/measure-performance.ps1` now publishes each measurement line as a notice in addition to writing the report
  file, so the dev2-versus-dev3 numbers are readable on the checks page and not only inside the artifact download. The
  lines contain a label and numbers only - no title, no URL.

## 2026-10-15 - dev3 CI is green on a08a9cf

- Run 38731531568 (`a08a9cf`) — the first green run of this change set: "Static checks and unit tests", "MSBuild project
  check", "Build x86", "Build x64", "Isolated real Brave smoke x86", "Isolated real Brave smoke x64",
  "Published baseline vs candidate resources (x64)" and "Package provenance and checksums" all succeeded.
- Real-Brave acceptance (Brave 155.1.97.56, Windows 10.0.26100, 96 DPI, both architectures) passed all 21 checks of
  the two phases, including the dev3 additions: hover mode shows the configured 40x40 px button over the selected tab,
  moving the pointer from the tab onto the button keeps it visible, an inactive tab and the title bar never satisfy the
  hover rule, a click on a hover-shown button closes exactly one tab through the unchanged validation path, the hover
  mode and the 40 px size survive a restart with Brave still open, and a corrupted settings file falls back to
  always-show with the documented 26 px default.
- Resource measurement, dev2 published EXE versus this candidate, one runner, 20 s samples each (browser-foreground:
  dev2 -> candidate): startup 108 -> 104 ms; idle CPU 3.23 -> 1.60 %; working set 48.3 -> 43.7 MiB; private bytes
  34.9 -> 31.5 MiB; handles 375 -> 370; UI Automation scan p50 63.1 -> 21.2 ms, p90 67.7 -> 23.5 ms. The no-browser
  scenario is unchanged by design (0.08 -> 0.00 % idle CPU, same working set within noise).
- Two harness defects were found and fixed by reading this run rather than assumed: the second phase inherited a Brave
  profile from the first phase, whose tabs the killed first phase had left behind — a restored session gave the phase a
  fixture it did not expect — and the phase asserted a tab count instead of probing it. It now uses its own private
  profile directory, probed the real tab height to choose the largest configured size that fits, and reported the
  fixture it actually found.
- Not verified here: the owner's own Windows desktop pass (physical DPI, mixed monitors, rapid clicking, tray and
  startup behaviour). `v0.1.0-dev3` is not tagged and PR #3 stays open and unmerged until that report exists.

## 2026-10-15 - correction: the real Brave tab does hold the 40 px maximum

- While fixing the smoke phase I wrote into the README and into the changelog bullet that a Brave v1.97.56 tab cannot
  hold the 40 px maximum, and treated the earlier "configured-phase scan did not confirm three tabs" failure as evidence
  of that. The green runs show the opposite: with the phase's own private profile the fixture has three standard
  horizontal tabs, and the 40x40 px button is displayed over the real selected tab in both architectures. The earlier
  failure was the inherited session, not the tab height.
- Corrected rather than left standing: the README paragraph now states the measured fact (a 40 px button with a 5 px
  inset fits the CI runner's Brave tab) and keeps the fail-closed rule for a tab that cannot hold the configuration. The
  product is unchanged - the rule and its unit tests were always correct; only my explanation of one CI failure was
  wrong. Lesson, again: a failed wait is evidence about the fixture until it is diagnosed, not about the product.

## 2026-10-15 - dev3 safety review and one documentation over-claim

- With CI green and the owner's manual pass still pending, I re-read the safety-critical paths of this change set with
  fresh eyes: `CloseGuard.Evaluate` and the close path in `ActiveTabController` (epoch check, foreground and modifier
  re-read immediately before injection, cooldown, click containment in the configured × area), the hover rule and its
  grace period (`HoverPolicy`, including the fail-closed handling of a clock that moves backwards), `ScanWorker`
  (one pending request, one running scan, a completion callback that cannot kill the worker thread), `SettingsStore`
  (cache, bounds, atomic replace, temporary-file cleanup) and `CloseButtonOverlay.ShowAt` (idempotent placement, native
  bounds re-verified after every move, hide on failure). No defect was found in the code, and nothing in the review
  needed a source change.
- One documentation over-claim was found and corrected: audit finding 2 said an oversized settings file is "left
  untouched on disk", which reads as an absolute promise even though the next settings change rewrites the file, as it
  does for any other file. The finding now states exactly that. Documentation only; no behaviour changed and the
  passing CI run on `50d9bc8` still stands for the code.
- State at this point: CI is green on `50d9bc8` (all eight jobs), PR #3 is open and unmerged, no tag exists, and the
  only outstanding gate is the owner's run of the CI-built EXE on their own Windows machine.

## 2026-10-15 - correction: the real-Brave evidence is 18 checks, not 21

- The entry above and the pull request description both said the real-Brave job "passed all 21 checks". Counting the
  `PASS` lines the job actually publishes gives 18 real checks (12 in the always-show phase, 6 in the configured
  phase) plus the job's closing summary line "PASS isolated real-Brave smoke; not a full desktop/DPI acceptance
  matrix." There were never 21; the number was written from memory instead of from the annotation.
- Corrected in the pull request description (18 checks, counted from the annotation) and recorded here. It changes no
  code, no test and no result: the same 18 checks passed in both architectures. Rule for the next report: a number
  that appears in a document must be counted in the evidence it cites, not remembered.

## 2026-10-15 - dev3 responsiveness work: the x no longer waits out the interval, and windows open faster

- Owner report: the delay before the button appears is significant, and the settings window lags when it opens. Owner
  direction: make it as fast as possible cleanly, then merge and tag. Both causes were removed at their source; no
  freshness window, deadline, budget, cooldown, stall rule or confirmation step was touched.
- **Hover arrival.** `ActiveTabController.ShouldScanNow` used to be consulted only after the interval gate, so a pointer
  that arrived just after a scan waited up to a full interval (500 ms default) for the button. The pointer gate now runs
  on every tick and remembers whether the pointer is in the tab strip; the outside-to-inside transition clears the
  interval, so one scan is submitted on that tick, and `EffectiveScanIntervalMs()` uses the documented minimum (300 ms)
  while the pointer stays inside the strip. Away from the strip nothing changes: no scan is submitted at all. The flag is
  re-armed on a mode change and whenever the pointer leaves, so a resting pointer cannot cause a scan loop (the worker
  also still allows only one scan at a time).
- **Windows coming to the front.** A foreground change (another Brave window, or Brave returning) now clears the interval
  for the next tick, so the confirmed overlay follows an alt-tab immediately instead of waiting out the interval that
  belonged to the previous window.
- **Window construction.** `SettingsForm` and `MainForm` added one row at a time to auto-sizing `TableLayoutPanel`s, so
  WinForms ran a full layout pass per row, and every `WrappingLabel`/`WrappingCheckBox` re-measured its text with GDI on
  every pass; each control also created its own `Font`, and `UiLayout.FitToWorkingArea` assigned `Size` even when the
  window already fitted. Now: the tree is built with layout suspended and laid out once, `UiLayout.TextMeasurement` caches
  the last measurement per control and drops it on `OnTextChanged`/`OnFontChanged`, the two windows share a handful of
  fonts by role (four in the settings window, five in the dashboard), and the fit check assigns nothing when nothing has
  to change.
- **New regression test.** `SettingsWindowBuildsOnceAndLaysOutStably` (Windows harness) opens the settings window, prints
  `INFO settings window built and shown in N ms` (the harness now forwards `INFO` lines as CI notices, so the number is
  readable on the checks page) and then asserts that two further layout passes leave every control's bounds exactly as
  they were - which is what a stale measurement cache would break.
- Local gates after the work, all clean: `tools/api-check.py` ("API audit clean: 66 types, 143 StringIds, 143 table rows",
  "Scope audit clean: 512 local declarations, no name is declared twice in one block chain") and `tools/verify-source.py`
  (passed, `v0.1.0-dev3`). The compiler-level proof is the CI run recorded below.
- Environment note, recorded for honesty: this session's local git refs were found rewound to `main` (`5917ded`) with the
  dev3 work left in the working tree. Nothing was lost - the branch tip `27cba73` was verified on the remote with
  `git ls-remote`, and the local branch was restored to it with `git reset --mixed origin/arena/8c38d759-xtension`, which
  does not touch `main` or the working tree. `main` was never modified by this session.

## 2026-10-16 - dev4 implementation: the × follows events, browsers become data, and everything is measured

This is the implementation record for `v0.1.0-dev4`, written before any CI run. The local environment has no C#
toolchain, no Windows desktop and no audio device, so nothing below is a verification claim: it is what was changed,
what was checked statically, and what still has to be proven by the runners.

**The real-time × (priority 1).** Three costs were traced in the source instead of being guessed from the interval.
(a) A refused scan request still advanced the "last request" timestamp, so a scan refused because the worker was busy,
or because the foreground browser was disabled or unsupported, cost a whole extra interval; the timestamp now advances
only when the worker accepted the request, and refusals are counted separately. (b) A tab switch could only be seen by
the next poll; a UI Automation `SelectionItemPattern.ElementSelectedEvent` subscription and a foreground change now
request an immediate scan, with an 80 ms floor and polling kept as the fallback, and neither source can authorise
anything - they only ask for an earlier scan. (c) The scan itself, which is UI Automation's cost and not ours, is now
measured rather than assumed: the harness times pointer-entry-to-visible through a shipped EXE, ten real-browser scans
give p50/p95/max, and the product publishes its own per-lane counters and percentiles in one bounded statistics line
(at most every 15 s or every 50 completed scans, never empty, with `samples=`). `scripts/measure-performance.ps1` reads
that line out of the log bytes the measured run appended and emits `MEASURE-LATENCY`, so the baseline and the candidate
are compared with the same method on the same runner.

**Stability and the repository-wide audit.** `docs/DEV4_AUDIT.md` records 18 findings with severity, evidence, root
cause, remediation and regression coverage: identity resolution per window and image name instead of a PID-keyed cache,
the refused-request interval, one scan lane per purpose, diagnostics that no longer present a cached result as the
current state, a settings-read failure that used to be silent, an empty browser mask that used to mean "Brave", a
backup import that reported preserved values it never wrote, the icon, the statistics cadence, worker shutdown
(accepted, with the reason), and repository hygiene. Two findings are deliberately left as "not verified" rather than
"fixed": the audio crackle and the visual appearance of the icon.

**Browsers.** Brave stays the only browser with acceptance evidence. `BrowserCatalog` holds the executable name, the
localized display name, the support level and the tab-access contract for each browser; the scanner refuses with
`ContractUnimplemented` and no tabs at all when the contract is not implemented, and `ActiveTabController` refuses a
window with no descriptor or a disabled browser. Chrome, Edge and Firefox are discovered and listed, and are marked
unsupported: nothing is inferred from shared Chromium ancestry, and no part of this release claims they work.

**Platforms.** macOS and Linux are documented as blocked with the exact mechanism-level blockers (no `WS_EX_NOACTIVATE`
equivalent, no `SetWinEventHook`, Accessibility/TCC permission as a first-class state, AT-SPI instead of UI Automation,
Wayland having no portable way to read another window's geometry or place a click-through overlay, and the
Windows-only managed `UIAutomationClient` in the .NET Framework 4.8 contract). No placeholder class, mocked browser or
untested executable was added, and both gates now fail a document line that names either platform without stating that
it is not supported.

**Profiles, diagnostics, appearance, backup, icon.** Profiles are a persisted marker over authoritative keys, so an
edited value survives a restart and reports `custom`; a profile can only choose cadences, never a budget, deadline,
window, cooldown or confirmation rule. Diagnostics separate current state from the last scan, name the hidden reason,
show the effective configuration and the per-lane statistics, and offer a copyable sanitized report. Appearance is
three bounded values (one of four colours, a border switch, 30-100% uniform window alpha which cannot change the hit
area). Backup export/import is validated, bounded, atomic and reports applied/rejected/preserved by name. The icon is a
reproducible multi-size ICO generated by `tools/generate-icon.py` (no image dependency), embedded as the EXE icon
(`/win32icon:`, `<ApplicationIcon>`) and as the managed resource the tray and windows load, with the painted icon kept
as a fallback and no way for the flag or the resource to disappear silently - both gates check it.

**Small decisions worth recording.** `tests/WindowsTests/BrowserBoundary.cs` was not written: its intended coverage is
already provided by `BrowserCatalogTests.cs` plus the new harness tests, and adding a third unexecuted blind-written
file would have duplicated coverage in the one place where duplication is most expensive. `scripts/test.ps1` already
globs `tests\UnitTests\*.cs`, so the four new unit suites need no script change; `Xtension.csproj` and
`scripts/test-windows.ps1` keep explicit lists and were updated for every new file. `.gitattributes` gained
`*.ico binary` and `*.png binary` so the new assets cannot be normalised as text. The README's profile table was
corrected against `AppInfo` while writing it (`balanced` is 500/250/100/2000 ms, `fast` uses the documented minima).

**Local checks, all clean at this point:** `python3 tools/api-check.py .` -> "API audit clean: 91 types, 202 StringIds,
202 table rows" and "Scope audit clean: 727 local declarations, no name is declared twice in one block chain";
`python3 tools/verify-source.py .` -> "Xtension static source checks passed (local mirror). Version: v0.1.0-dev4"
(including the new icon, EXE-icon and platform-claim rules). Every `.ps1` was re-checked for CRLF and ASCII after
patching, and `tools/api-check.py` complaints were resolved by renaming code, never by editing the check.

**Not claimed:** nothing was compiled or run here. The audio crackle was not reproduced and the notice-window change is
a candidate fix, not a verified one. The icon was not looked at on a scaled tray. Chrome, Edge and Firefox are not
supported. macOS and Linux are not supported.

## 2026-10-16 - dev4 verification: what the gates found, and how the candidate got there

Written after the candidate had been compiled and executed by the repository's own CI for the first time. Nothing here
replaces the implementation entry above; it is the record of what verification actually caught, in the order it
happened, because that is the part a reader cannot reconstruct from the final diff.

1. **The static check said nothing at all.** The first CI run failed in fourteen seconds with no annotation and no log
   reason. Two defects in the check itself: the new required-file entries used doubled path separators, and the new
   path comparisons were regular expressions in which `\x` is a hex escape, so `assets\xtension.ico` was an invalid
   pattern that terminated the script. Both are fixed, and a `trap` now reports the collected failures plus the error
   as a check annotation, so a red static job explains itself instead of needing a log download.
2. **The candidate did not compile, twice.** `Profiles.Clone` was called on three paths and never written, and
   `AppIcon` used `SystemInformation` (in `System.Windows.Forms`) after importing only `System`, `System.Drawing`,
   `System.IO` and `System.Reflection`. Both are the cost of writing C# with no compiler available; both are now fixed
   and both are named in `docs/DEV4_AUDIT.md`.
3. **Three unit tests failed, and the tests were the wrong ones.** Two of the four new suites had expectations written
   before they had ever run: `MetricsTests` asserted a request count it never produced, and `BackupTests` assumed the
   rejected-key report keeps insertion order while `SettingsBackup` sorts it deliberately so the same document always
   produces the same list for the user to read. The third was neither: `BrowserCatalogTests` expected an unsupported
   browser never to be "enabled", and the product only required the mask bit. Rather than weaken the test, the product
   was changed: `IsEnabled` now requires the browser to be supported as well, so a future call site that forgets the
   support check still cannot scan a browser this build does not implement.
4. **The Windows harness found four real defects.** Four settings-window tests died with
   `ObjectDisposedException: Cannot access a disposed object.` because `AppIcon` cached *live icon handles* and returned
   the same instance to every caller, so the icon test's own `using` destroyed the handle every window still used. The
   same round exposed the painted fallback returning an icon whose handle it had already destroyed, and a dev3 scanner
   test that called the handle overload on a synthetic window which is not an enabled browser - the gate refused it,
   correctly, and the test was calling the wrong overload. All three are fixed: the icon cache holds bytes and hands
   out a caller-owned instance per call, the fallback encodes and decodes a real 32-bit ICO (layout validated against
   an independent ICO reader before being written in C#), and the scanner test now asserts the refusal and then
   exercises the walk through the contract overload while still saying in its own comments that a synthetic window is
   not evidence that a real browser works.
5. **All local gates were re-run after every change** (`tools/api-check.py`, `tools/verify-source.py`, plus three
   purpose-written heuristics: a tree-sitter member-existence check, an arity check for qualified calls on repository
   types, and an unqualified-BCL-type check). Every complaint was fixed in the code, never in the check. The icon asset
   was additionally verified with an independent reader: nine frames, all 32-bit, all inside the file, and 252/256,
   564/576, 960/1024 and 2040/2304 visible pixels at 16, 24, 32 and 48 pixels.
6. **Local git state was rolled back once by the environment.** After the first push, the checkout was found rewritten
   to a fresh clone of the session branch, with the work present in the working tree but the local commits gone; the
   remote still held them. The local branch was restored with `git reset --mixed origin/arena/9ec9bbb9-xtension`, which
   touches neither the working tree nor `main`, and the work continued from the remote tip. `main` was never modified
   by this session and no force-push was used at any point.

Not claimed: at the time of writing, the CI jobs after these fixes have not finished, so this entry records what was
found and what was changed - not that the candidate is green. The final report records the runs.
