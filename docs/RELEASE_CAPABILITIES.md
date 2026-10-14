## Capabilities

- Shows one custom × on the selected horizontal tab of a Brave window, at the tab's right edge.
- The button moves when the selected tab changes, and is hidden whenever the active tab is not confirmed.
- A click sends Ctrl+W only after a fresh UI Automation scan confirms that the click landed on the × of the one selected horizontal tab, and Brave is still the foreground window.
- The overlay can be turned off from the tray menu or the dashboard. Exiting the app is a separate action.
- Startup with Windows is off by default. It is a per-user setting and needs no administrator rights.

## Limitations

- Windows only, x86 and x64. No macOS or Linux support.
- Depends on Brave's UI Automation tree. After a Brave update that changes the tree, the app hides the button rather than guessing.
- Brave's "Always hide tab close button" setting, if your Brave version provides it, hides native close buttons. Xtension does not change that setting. If you turn the × off or exit Xtension, native close buttons may stay hidden until you change that Brave setting.
- A tab switch in the short window between confirmation and keystroke cannot be prevented by any available API. This residual risk is recorded in DESIGN_LOG.md.
- Prerelease. Real-Brave behaviour is verified only where the release's CHANGELOG Verification section says so.

## Candidate verification boundary

The dev2 candidate corrects explicit UIA cache navigation and adds synthetic Windows regression tests. A fixture with two tabs is not Brave: no claim is made that the user's exact failure, Brave updates, click-to-close, physical DPI/multiple monitors, or all Vietnamese text rendering has been accepted on a real installation. Current outcome and release decision are recorded in `docs/DEV2_AUDIT.md` and the changelog. Scan failures include privacy-safe operation/type/HRESULT metadata; raw provider messages are not logged.
