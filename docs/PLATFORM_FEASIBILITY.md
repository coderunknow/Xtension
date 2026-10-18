# Platform feasibility — no non-Windows support in dev5

Assessment date: 2026-10-18. macOS and Linux (X11 and Wayland) are **unsupported**.
This is source/API feasibility research, not a port or a runtime certification.

## What must survive a port

The product is not merely a close shortcut. It must identify exactly one selected *horizontal* browser tab, bind an
identity and bounds to the displayed button, display without taking activation, receive a genuine click inside that
button, revalidate identity/foreground/geometry with bounded age and deadlines, reject modifiers and stale epochs,
and only then close the intended tab. Accessibility notifications remain advice. Permission denial, disappearing
objects, inconsistent selection and stalled providers must all refuse input. A toolkit displaying a transparent panel
is not enough to establish those properties.

The current hard dependencies are `System.Windows.Forms`, Framework 4.8, UIAutomationClient/Types, Win32 foreground,
DPI and window APIs and SendInput. `Xtension.csproj`, `NativeMethods`, `UiaTabScanner`, `KeyboardInjector` and the
Windows-only build/test scripts show why recompiling the EXE is not a port. A native host per platform could reuse
pure core algorithms and test vectors, with platform-specific identity, geometry, time and input contracts. Windows
must retain its current host, settings schema, localization and release matrix. A wholesale runtime migration is not
justified by this release's evidence.

## macOS — unsupported; technically plausible separate native host

- Candidate access layer: AXUIElement attributes/actions and AXObserver, with explicit accessibility-trust/TCC states.
  Each browser needs independently sampled AX tab selection, role, parentage, identity and screen-bounds evidence;
  UIA runtime IDs cannot be translated into AX identities by assumption. Stale/detached AX references must refuse.
- Candidate overlay: AppKit nonactivating NSPanel, screen/space tracking, explicit hit region, no key/main-window
  acquisition. Test full-screen spaces, native tabbed windows, display coordinate origins, Retina backing scale and
  focus transitions rather than mapping Windows coordinates by arithmetic alone.
- Candidate close: freshly validated accessibility action where the browser exposes an exact target action, or
  carefully permission-gated event posting with Command-W. Neither is assumed equivalent to Ctrl-W; secure-input,
  trust denial/revocation and input-delivery refusal must be first-class states. Avoid requesting broader privileges
  simply to pass tests.
- Packaging would require a new native application bundle, architecture decisions (arm64/x64), signing/notarization
  and entitlements review. No certificates or signing material belong in Git. A real GUI runner with permission
  approval and disposable browser profiles is a prerequisite; this session has neither a desktop nor a permission
  provisioning mechanism. Relative effort is high: a new host, three native adapters, browser contracts and GUI matrix,
  not a compiler flag. No untestable adapter is added.

## Linux X11 — unsupported; plausible only with a constrained desktop contract

- AT-SPI2 over D-Bus supplies selected-child references, not UIA elements. Its Selection interface explicitly allows
  multiple selected children; require exactly one selected tab under a confirmed browser tab list, validate the bus
  owner/object lifetime, state/geometry and inaccessible providers. Browser accessibility availability must be tested,
  not inferred from its Windows implementation.
- X11/EWMH active-window and root-coordinate queries, override-redirect or WM-managed non-focusing overlay, input
  shapes and XTest-style key injection are possible design candidates. WM policy, compositors, toolkit focus behavior,
  scaling and virtual desktops differ. X11's broad input access is not proof that a particular browser is foreground;
  authorization still needs fresh independent window/browser identity checks. XWayland does not grant authority over
  native Wayland windows and must not be marketed as a native Wayland solution.
- A separate supported runtime/GTK or native host and AT-SPI/window/input adapters are required. Packages would need
  explicit distro/library/architecture bounds and real WM/browser CI (nested test server plus actual compositor,
  and later physical display tests). Relative effort is high but a single declared WM/X11 configuration is a feasible
  research milestone. Running headless browser DOM tests is not overlay acceptance.

## Linux Wayland — unsupported; ordinary desktop client is insufficient

- Core stable xdg-shell uses surface-local geometry and parent-relative popup placement; it does not supply a normal
  client with arbitrary foreign toplevel coordinates, placement over another application's tab, or unrestricted
  foreground key injection. This is a security boundary, not a missing P/Invoke declaration.
- Layer-shell provides output-edge anchoring and keyboard-interactivity choices on compositors implementing that
  protocol; it does **not** by itself supply exact foreign-tab coordinates/identity or portable focused-window input.
  Compositor extensions, a trusted shell/plugin, or user-approved portals/remote-control flows would need separate
  integration and UX. Availability differs across compositors; do not generalize wlroots to all desktops.
- AT-SPI selection alone cannot bridge the window placement/input gap. A browser extension could use browser tab IDs
  for exact closing, but cannot generally add a button to native browser chrome; an in-page/content overlay changes the
  central product requirement and is not a transparent replacement. A compositor plugin is a substantially different
  trust/distribution model, not a dependency to add casually.
- Acceptance would require a declared compositor/protocol matrix, permissions denied/revoked tests, scale/output
  mapping and real native browser/overlay close tests. Relative effort/maintenance is very high and portability is
  uncertain. No placeholder binaries, dummy adapters or support flags are delivered.

## Options and gates

1. **Retain Windows host (selected for dev5).** Lowest regression risk; test real additional browsers and strengthen
   existing authorization. No framework replacement, no runtime dependencies.
2. **Separate native host + small pure safety model.** Feasible exploratory direction after native API spikes produce
   concrete tab identity/geometry and nonactivation evidence. Preserve Windows and migrate settings only through a
   versioned, tested schema; do not abstract away a platform-specific safety condition.
3. **Extension-assisted/compositor-specific design.** Potentially improves exact-tab closing, but changes deployment,
   permissions and sometimes native-chrome placement. Needs an explicit future product decision and threat model.

Before any platform is offered: actual selected-tab contract, bounded failures, genuine nonactivating overlay,
permission denial/refusal tests, click closing exactly the intended runtime identity, rapid selection/window races,
move/minimize/desktop/display/scale tests, supported architectures, provenance/checksum packaging and runnable GUI CI
must all exist. A build or a panel demo cannot substitute. Cost estimates above are relative engineering scope,
not invented delivery dates. No non-Windows implementation passes these gates today.

## Primary-source checks

Retrieved via GitHub API on the assessment date (source inspection only):

- [AT-SPI Selection XML](https://github.com/GNOME/at-spi2-core/blob/main/xml/Selection.xml), blob `d574ae92085c0b60f860c01c40ac7dfa49674521`: NSelectedChildren/GetSelectedChild and bus-name/object-path identity.
- [stable xdg-shell protocol](https://github.com/wayland-mirror/wayland-protocols/blob/main/stable/xdg-shell/xdg-shell.xml), blob `f8b78eea7bf3bd686753befd30e83f757326aed0`: parent-relative positioner and surface-local window geometry.
- [wlr-layer-shell protocol](https://github.com/swaywm/wlr-protocols/blob/master/unstable/wlr-layer-shell-unstable-v1.xml), blob `d62fd51e90d18247a5b8097395c3291b7099a380`: output anchoring and keyboard-interactivity boundary.
- [WinForms upstream](https://github.com/dotnet/winforms): Windows desktop framework; changing the runtime alone does not replace this repository's native dependencies.
- [AppKit binding surface](https://github.com/dotnet/macios/blob/main/src/appkit.cs), blob `8fb0852adb21931c3c6f3d1a0ee77a63ac6c4bce`: candidate native-host API reference, not evidence of browser behavior or granted permissions.

No external browser/desktop runtime was executed for this feasibility report. All browser-specific native contracts
above are prerequisites, not verified capabilities.
