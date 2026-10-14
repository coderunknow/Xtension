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
