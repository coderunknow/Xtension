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
