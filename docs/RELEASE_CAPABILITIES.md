## Capabilities

- Shows one custom × on the selected horizontal tab of a Brave window, at the tab's right edge, at a size you choose (18-40 logical pixels) with the right inset you choose (0-12 logical pixels).
- Visibility modes: `always` (shown whenever the active tab is confirmed) and `hover` (shown only while the pointer is over the confirmed selected tab or on the button, with a bounded keep-time).
- A settings window with every option, reachable from the dashboard and the tray menu. Changes apply to the running app and are saved immediately.
- Interface language toggle between Vietnamese (default) and English, applied immediately to every user-visible string.
- Settings file `%LOCALAPPDATA%\Xtension\settings.ini`; unknown keys are preserved and out-of-range values are replaced by documented defaults.
- The button moves when the selected tab changes, and is hidden whenever the active tab is not confirmed.
- A click sends Ctrl+W only after a fresh UI Automation scan confirms that the click landed on the × of the one selected horizontal tab, and Brave is still the foreground window.
- The overlay can be turned off from the tray menu or the dashboard. Exiting the app is a separate action.
- Startup with Windows is off by default. It is a per-user setting and needs no administrator rights.

## Limitations

- Windows only, x86 and x64. No macOS or Linux support.
- Depends on Brave's UI Automation tree. After a Brave update that changes the tree, the app hides the button rather than guessing.
- Brave's "Always hide tab close button" setting, if your Brave version provides it, hides native close buttons. Xtension does not change that setting. If you turn the × off or exit Xtension, native close buttons may stay hidden until you change that Brave setting.
- A tab switch in the short window between confirmation and keystroke cannot be prevented by any available API. This residual risk is recorded in DESIGN_LOG.md.
- Hover mode is a display rule, not a security boundary: a click still requires the fresh close validation.
- The settings window has not been visually reviewed by a human on a real desktop; only measured layout assertions cover it in CI.
- Prerelease. Real-Brave behaviour is verified only where the release's CHANGELOG Verification section says so.

## Verification boundary

Windows CI covers both architectures, the expanded core test set, the synthetic Windows UIA/layout/settings/injection tests per architecture, packaging provenance/checksums, the real-Brave smoke (including the hover and configured-size phase for dev3), and a resource comparison of the published previous release against the candidate on one runner. The old cache-less navigation reproduces `InvalidOperationException` / `0x80131509`; the corrected scanner succeeds.

Actual x86/x64 EXEs were exercised against official Brave v1.97.56 (binary product version 155.1.97.56) in isolated profiles on Windows OS 10.0.26100.0 at 96 DPI. Verified: button visibility/placement, tab switches, multiple windows, focus changes, Shift/Alt/Win rejection, mouse close of exactly the selected runtime identity, minimize/restore/move/resize/maximize, twelve tabs and long synthetic title. The owner's exact Brave build and error were not available.

Still unverified: physical mixed-monitor DPI, native 32-bit Windows, rapid-click and tab-switch race stress, pinned/vertical/scrolled layouts on real Brave, actual hung-provider recovery, tray/startup integration and graceful exit. Scaled-font layout assertions are not visual certification of every Vietnamese glyph. No universal bug-free claim is made. Raw provider messages, browsing titles and URLs are not logged; diagnostics uses operation/type/HRESULT and sanitized application frames. Optional external review services did not perform a review because of credit/quota/eligibility limits.

See `docs/DEV3_AUDIT.md` for this candidate's findings and remaining risk, and `docs/DEV2_AUDIT.md` for the previous release. Release assets are x86/x64 EXEs, a source ZIP and SHA256SUMS.txt. The tag workflow packages exact-commit builds and downloads published assets again to verify hashes.
