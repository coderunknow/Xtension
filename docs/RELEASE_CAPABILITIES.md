## Capabilities

- Windows x86/x64, C# 5, .NET Framework 4.8. No new runtime dependency or framework replacement.
- One non-activating custom × on a confirmed selected horizontal browser tab. Logical size 18–40 px, inset 0–12 px,
  bounded opacity/colour/border, always/hover visibility, performance profiles, Vietnamese/English, tray/dashboard/
  diagnostics/settings, per-user startup (off by default), validated bounded settings import/export.
- Close requires a fresh bounded UIA scan, the same nonempty runtime identity as the displayed target, a click inside
  the validated ×, correct foreground browser, enabled provider, unchanged epoch/window geometry/DPI, deadlines,
  modifier rejection and cooldown. A held press is cancelled on placement/identity change. Hover never authorizes.
- Settings ownership is centralized; disk/cache writes are transactional. Unknown keys survive rewrites and now also
  export/import round trips. Backups are key=value INI, not JSON. Inspect user-authored unknown values before sharing.
- Brave remains the default browser. Chrome is an independently tested opt-in addition. Edge is unsupported after
  reproducible depth-budget acceptance failure; detection remains, its scanner is disabled, and explicit real-EXE
  refusal tests replace any support claim. Firefox is unsupported/unimplemented. The preference for two additional
  browsers was not safely achieved; sharing Chromium ancestry is not support.

## Verification boundary

The repository CI runs the actual PowerShell source checks and core suites, C# 5/Framework builds for both process
architectures, MSBuild validation, synthetic Windows UIA/layout/settings/injection/lifecycle regressions, package
provenance and negative tests, three-repeat published-dev4-vs-candidate observations and real browser acceptance on
both architectures. The release workflow requires successful exact-commit merged-main CI, then tests the exact
release-build EXEs against supported browsers before packaging and publication. Published bytes are re-downloaded
and checked against SHA256SUMS.txt. These are gates, not claims that a pending workflow already passed.

`docs/DEV5_AUDIT.md` records observed run IDs, commits, results, measurements and limitations. Earlier releases' audit
records remain historical evidence, not certification of this candidate. Synthetic windows are not real browsers.

## Limitations

- Windows only. macOS and Linux X11/Wayland are unsupported; see `docs/PLATFORM_FEASIBILITY.md` for separate access,
  permission, overlay, foreground/input and testability boundaries. No placeholder port or non-Windows binary.
- Browser UIA changes fail closed. Pinned/vertical/scrolled-out layouts are not supported targets. Exact tested browser
  versions and single-display CI results do not certify arbitrary versions, policies or physical desktops.
- There is an unavoidable scheduling gap between fresh validation and OS input delivery; selection/foreground may
  change then. Runtime identity reduces pre-validation target replacement risk; it cannot make SendInput atomic with
  browser selection. No universal race-free or bug-free claim.
- Physical mixed-monitor DPI, native 32-bit Windows, a genuinely hung provider and visual/icon certification remain
  unverified unless the audit supplies new evidence. Bounded background threads keep UI responsive but a truly hung
  lane is not forcibly aborted; restart may be necessary.
- Real startup registry integration, tray shell interactions and graceful process shutdown still need explicit
  acceptance beyond control-level tests. No certificate/signing/SmartScreen trust guarantee.
- Native close buttons and browser settings are never changed. Brave's optional close-button setting may remain in
  effect after disabling or exiting Xtension; the appearance-settings shortcut is Brave-specific.
- Owner reports historical audio distortion is no longer observed. The cause remains unconfirmed; retain sound-free,
  non-activating notices without claiming a proven causal fix.
