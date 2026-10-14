# AGENTS_LOG.md — append-only record of agent work

Rules: append new dated entries at the bottom. Do not rewrite or delete earlier entries. Never record tokens, credentials, or private tab or window titles.

## 2026-10-14 — Release preparation for v0.1.0-dev1

- Audited the uploaded source zip. Found: hard-coded version literals in several files; fixed-pixel overlay thresholds that fail at 200% DPI; a synchronous UI-thread UI Automation scan every 650 ms (a hung Brave froze the tray); a close path that did not re-check foreground at click time; a close check that compared overlay bounds with expected bounds, so valid clicks could look stale; tab titles written to the log; and a repeated `Process.GetProcessById` call on every check.
- Rewrote the source as focused files: `src/Core`, `src/Interop`, `src/Uia`, `src/App`, `src/UI`. Introduced a fail-closed `CloseGuard`, an epoch-based state machine, a dedicated scan thread with a single pending slot, a 5 s stall detector, and a non-activating overlay.
- Added single-version generation from `VERSION`, an explicit csproj compile list, build/test/verify/package scripts, unit tests for the pure core, and CI and tag-release workflows.
- Verification performed in this environment: no C# compiler and no PowerShell were available. Checks run: tree-sitter C# parser over all 31 source and test files (syntax only, no errors); a cross-reference script for `Class.Member` references among project types (no unresolved references, after a false positive for generic `Assert.AreEqual<T>`). Not run: csc, msbuild, PowerShell scripts, unit tests, GUI, Brave.
- Removed `Xtension-v0.1.0-source.zip` from the working tree. The zip is recoverable from commit `60326da`. The release source ZIP is now built from git by `scripts/package.ps1`.
- Left LICENSE unchanged.
