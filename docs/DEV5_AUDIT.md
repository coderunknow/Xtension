# dev5 audit and acceptance record

Date: 2026-10-18. Work in progress; publication is gated, not implied by this document.
Baseline: main `685975ec088ac28a722e004d6354c9fc1cbd6e63`, dev4 tag `15900f5`.

## Findings recorded before CI

| ID | Severity | Evidence and root cause | Impact / reproduction | Remediation and regression | Status / residual risk |
| --- | --- | --- | --- | --- | --- |
| D5-01 | High | `TabInfo`, `CloseGuard`, `OnOverlayClicked`: displayed identity was not carried into close validation | select a different tab into identical bounds before validation; old guard accepted it | capture UIA runtime ID from displayed result, compare exact nonempty array against fresh result; unit regression with identical geometry/different ID | Fixed, Windows verification pending; post-validation input race remains |
| D5-02 | Medium | `SettingsStore.SetMany` mutated `_values` before validation/write finished | blocked temporary path or later invalid key leaves earlier values in cache; later Save persists failed changes | copy, validate, atomically write then publish cache; regression for failed I/O and invalid batches | Fixed, execution pending; no cross-process settings editor synchronization |
| D5-03 | Medium | controller copied only selected fields on ApplySettings, returned its mutable settings reference | changed colour/opacity/border/language appear in overlay but diagnostics retain old values | copy ownership in/out; apply all fields and invalidate on appearance change; Windows snapshot regression | Fixed, execution pending |
| D5-04 | Medium | scanner checked contract enum only, not descriptor implementation; walked any TabItem without tab-list parent | unsupported Chrome descriptor could enter shared scan overload; unrelated TabItem could look horizontal | descriptor implementation gate and direct Tab parent contract; actual browsers and synthetic fixture must verify it | Fixed, runtime pending |
| D5-05 | Medium | `RequestImmediateTick` changed UI-owned flags/64-bit timestamps on UIA callback thread | concurrent event delivery/read, including x86 torn timestamps | coalesced callback writes timing/state on UI thread only | Fixed, synthetic/real stress pending; events remain advisory |
| D5-06 | Low | WinEvent hook range 3–22 includes unrelated system events; handler did not filter | menu/capture events ask for unnecessary scans | only foreground and minimize start/end trigger callback | Fixed by source; no measured CPU claim |
| D5-07 | Medium | latency harness caught failed hover-hide and returned success; scan sampler returned silently on failed scan | acceptance could pass without advertised latency evidence | require every sample and hide prerequisite, fail rather than skip | Fixed, real execution pending |
| D5-08 | Low | close completions excluded from metric Record although three lanes were documented | close lane silently absent from scan sample population | record every completed lane using same bounded buffer | Fixed, verification pending; percentiles still aggregate, not per-lane display |
| D5-09 | Informational | dev4 capabilities points at dev3 audit, README says backup JSON while implementation is key=value | misleading operational documentation | update current docs, retain historical logs | In progress |

## Browser acceptance gate

Brave retains baseline acceptance only until new run. Chrome/Edge candidate contracts are experimental pending real CI; they use exact executable identity, direct UIA Tab → TabItem, unique known SelectionItem selected state, runtime identity, bounded walk and existing horizontal geometry. Merely setting the catalog entry is not acceptance. Default remains Brave-only. Each actual browser must pass an isolated-profile packaged-EXE placement/close/refusal/hover/persistence test on both process architectures. Firefox is unsupported: Gecko UIA mapping is not implemented or verified. No placeholder provider.

## Verification

Local tools/api-check.py and tools/verify-source.py pass (aids, not compilation). Actual scripts, x86/x64 builds, synthetic Windows harness, package provenance, performance and real-browser acceptance are pending Windows CI. Direct local dev4 asset download failed at disallowed release-assets host; no local hash is claimed. CI must verify it before execution. No tag or release created.

## Additional trace findings

| ID | Severity | Evidence / root cause / reproduction | Remediation and regression | Status / residual risk |
| --- | --- | --- | --- | --- |
| D5-10 | High | overlay `_armed` survived real moves, and same-bounds identity replacement was not a mouse gesture boundary; mouse-down on A then target changes before mouse-up could click B | cancel press on placement or displayed-identity change; copy actual displayed identity before hiding; synthetic down/change/up versus normal click | Fixed; pending new CI; input after fresh validation still has unavoidable OS scheduling race |
| D5-11 | Medium | `TabSelectionWatcher.Unregister` cleared root/flag but retained `_registered`; A → zero → A skipped Register forever | reset registered HWND on unsubscribe, handle disposed wake safely; actual synthetic UIA subscribe/unsubscribe/resubscribe regression | Fixed; pending CI; a genuinely hung registration remains a bounded abandoned background thread, not forcibly aborted |
| D5-12 | Medium | `SettingsForm.ImportBackup` set result text, then LoadFromHost/RefreshStatus overwrote it with visibility mode | refresh first and show actual import result last; test through the same internal non-dialog method the file dialog calls | Fixed; pending CI; no redesign |
| D5-13 | Medium | release workflow previously allowed any version-matching prerelease tag without successful exact-commit main CI, and rebuilt EXEs without real-browser release-byte acceptance | tag gate requires current merged main SHA and successful push CI for that SHA; six release browser jobs use exact artifacts that publish packages | Implemented, end-to-end pending; no old tags/assets altered |

## Feature ranking and platform decision

1. Opt-in Chrome/Edge: high value for existing workflow, moderate provider maintenance, no new runtime dependency; acceptance is actual isolated-profile EXE placement, exact single-tab close, disabled/modifier refusal and lifecycle on both architectures. In progress, not yet verified.
2. Complete future-key backups: concrete gap in dev4 export, small bounded core implementation. Export includes unknown store keys except schema metadata; round-trip regression exercises export/parse/apply. Implemented, CI pending. Backups contain configuration, including user-authored unknown values; inspect before sharing.
3. Global shortcuts: deferred, little repository evidence, input/focus conflicts and unnecessary hooks.
4. New platform host: deferred, high scope/risk and no native GUI acceptance environment.

macOS and Linux X11/Wayland remain unsupported. See [platform feasibility](PLATFORM_FEASIBILITY.md) for native accessibility/overlay/input boundaries, alternatives, relative costs and real-browser/permissions/packaging gates. No fake port or placeholder artifact is delivered.
