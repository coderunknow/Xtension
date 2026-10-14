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
