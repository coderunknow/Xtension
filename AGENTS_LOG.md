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
