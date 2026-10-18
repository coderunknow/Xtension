using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>
    /// Owns the overlay state machine. All state is touched only on the UI thread. UI Automation runs on three worker
    /// threads: one for periodic overlay scans, one for close validation so a × click never waits behind a scan, and
    /// one for the dashboard's background scan so a slow diagnostics scan can never delay the scan that makes the ×
    /// appear.
    ///
    /// Safety model (unchanged by dev4):
    /// - The overlay is shown only after a Ready scan of the current foreground supported-browser window.
    /// - Every state change bumps an epoch. Results from an older epoch are discarded.
    /// - A click sends Ctrl+W only if a fresh scan confirms the click landed on the × of the one selected horizontal tab.
    /// - After a keystroke, the overlay is hidden and clicks are ignored for CloseCooldownMs.
    ///
    /// dev4 additions:
    /// - Any browser the user enabled, identified by process image name, instead of one hard-coded executable.
    /// - Event notifications (a browser tab becoming selected, and the Windows foreground/minimize events) request an
    ///   earlier scan. They can only ask for a scan; they never decide eligibility and never authorise a close, and
    ///   polling continues as the fallback when a source is unavailable.
    /// - Bounded response-time statistics, so the diagnostics window and the CI measurement can show what the
    ///   event-to-visible path really costs instead of assuming it.
    /// </summary>
    internal sealed class ActiveTabController : IDisposable
    {
        private readonly CloseButtonOverlay _overlay;
        private readonly Control _invoker;
        private readonly ScanWorker _overlayWorker;
        private readonly ScanWorker _closeWorker;
        private readonly ScanWorker _dashboardWorker;
        private readonly ForegroundWatcher _foregroundWatcher;
        private readonly TabSelectionWatcher _tabWatcher;
        private readonly HoverTracker _hover = new HoverTracker();
        private readonly AppSettings _settings;
        private readonly ScanMetrics _metrics = new ScanMetrics();
        private readonly Func<IntPtr, ScanResult> _scan;

        private volatile bool _disposed;
        private readonly object _wakeGate = new object();
        private bool _wakePending;
        /// <summary>An event asked for a scan sooner than the configured interval. Cleared when a scan is submitted.</summary>
        private bool _immediatePending;
        private long _immediateAtMs = Clock.Never;
        /// <summary>When the last eligibility-changing event arrived, for the event-to-visible measurement.</summary>
        private long _eventAtMs = Clock.Never;

        private bool _enabled;
        private bool _dashboardVisible;
        private bool _browserInForeground;
        private BrowserKind _foregroundBrowser;
        private bool _foregroundBrowserDisabled;
        /// <summary>True when the foreground window belongs to a browser this build knows but does not support.</summary>
        private bool _foregroundBrowserKnown;
        private bool _ownWindowInForeground;
        private bool _stalled;
        private bool _stallLogged;
        private long _epoch;
        private long _nextRequestId;
        private IntPtr _foreground = IntPtr.Zero;
        private IntPtr _targetWindow = IntPtr.Zero;
        private IntPtr _lastBrowserWindow = IntPtr.Zero;
        private StringId _idleReason = StringId.IdleStarting;
        private StringId _notice = StringId.None;
        private ScanResult _latest;
        private ScanOutcome _loggedOutcome = ScanOutcome.Idle;
        private string _loggedError;
        private long _lastOverlayRequestMs = Clock.Never;
        private long _lastDashboardRequestMs = Clock.Never;
        private long _lastConfirmedMs = Clock.Never;
        private long _lastScanCompletedMs = Clock.Never;
        private long _lastMetricsLogMs = Clock.Never;
        /// <summary>Completed scans when the last statistics line was written, for the bounded secondary schedule.</summary>
        private long _lastMetricsLogCompleted;
        private long _lastSentMs = Clock.Never;
        private bool _closeInFlight;
        private long _closeRequestId;
        private long _closeStartedMs = Clock.Never;
        private string _overlayFailure = string.Empty;
        private Rectangle _hoverRegion = Rectangle.Empty;
        /// <summary>Rectangle the overlay was last placed and verified at, so a real move can be logged once.</summary>
        private Rectangle _placedTarget = Rectangle.Empty;
        private bool _placedVisible;
        private int[] _displayedTabRuntimeId;
        /// <summary>Hover mode: was the pointer inside the tab strip on the previous tick?</summary>
        private bool _stripPointerInside;

        // Last state that was pushed to the views, so an uneventful tick costs nothing.
        private IntPtr _notifiedForeground = new IntPtr(-1);
        private bool _notifiedBrowser;
        private bool _notifiedStalled;
        private bool _notifiedEnabled;
        private bool _notifiedCloseInFlight;
        private bool _notifiedVisible;
        private StringId _notifiedIdle = (StringId)(-1);
        private StringId _notifiedNotice = (StringId)(-1);
        private string _notifiedOverlayFailure;
        private ScanResult _notifiedLatest;

        /// <summary>Raised on the UI thread whenever something visible may have changed.</summary>
        public event EventHandler Changed;

        /// <summary>Raised on the UI thread when a close keystroke could not be delivered.</summary>
        public event Action<string> CloseFailed;

        public ActiveTabController(CloseButtonOverlay overlay, Control invoker, AppSettings settings)
            : this(overlay, invoker, settings, null)
        {
        }

        /// <summary>
        /// Test seam: the scan delegate is private to the process and only a synthetic harness supplies one, so a test
        /// can drive the state machine without a browser.
        /// </summary>
        internal ActiveTabController(CloseButtonOverlay overlay, Control invoker, AppSettings settings,
            Func<IntPtr, ScanResult> scanOverride)
        {
            _overlay = overlay;
            _invoker = invoker;
            _settings = settings == null ? AppSettings.Defaults() : settings.Copy();
            _settings.Normalize();
            _enabled = _settings.OverlayEnabled;
            _scan = scanOverride == null ? new Func<IntPtr, ScanResult>(ScanWindow) : scanOverride;
            _overlayWorker = new ScanWorker("Xtension UIA overlay scan", Post, ScanOverlay);
            _closeWorker = new ScanWorker("Xtension UIA close validation", Post, ScanOverlay);
            _dashboardWorker = new ScanWorker("Xtension UIA dashboard scan", Post, ScanOverlay);
            _overlay.Clicked += OnOverlayClicked;
            // Both event sources are best effort and are cleaned up in Dispose. A failure leaves polling only, which
            // can delay the × and can never authorise anything.
            _foregroundWatcher = new ForegroundWatcher();
            _foregroundWatcher.Changed += OnForegroundEvent;
            _tabWatcher = new TabSelectionWatcher(OnTabSelectionEvent);
            _overlay.ApplyAppearance(_settings);
        }

        /// <summary>Scan delegate that carries the configured × geometry into every scan decision.</summary>
        private ScanResult ScanOverlay(IntPtr window)
        {
            return _scan(window);
        }

        private ScanResult ScanWindow(IntPtr window)
        {
            // The browser entry is resolved first and passed in, so the scanner can refuse a contract it does not
            // implement instead of applying another browser's assumptions to the window.
            BrowserDescriptor descriptor = ProcessIdentity.Descriptor(window, false);
            if (descriptor == null || !BrowserCatalog.IsEnabled(_settings.Browsers, descriptor.Kind))
            {
                ScanResult refused = ScanResult.Idle(window, ScanOutcome.ContractUnimplemented,
                    ScanMessageKey.BrowserUnimplemented);
                refused.Dpi = NativeMethods.GetWindowDpi(window);
                return refused;
            }
            return UiaTabScanner.Scan(window, descriptor, _settings.ButtonSizeLogical, _settings.ButtonInsetLogical);
        }

        /// <summary>Current effective settings copy. Callers must go through the Set* methods to apply changes.</summary>
        public AppSettings Settings
        {
            get { return _settings.Copy(); }
        }

        /// <summary>Poll cadence for the current state and profile. Used to set the UI timer interval.</summary>
        public int PollIntervalMs()
        {
            ProfileCadence cadence = Profiles.CadenceFor(_settings.Profile);
            return _settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab
                ? cadence.HoverPollMs : cadence.ForegroundPollMs;
        }

        /// <summary>
        /// Asks for a tick to run as soon as the UI thread can, because an event says the frame may have changed.
        /// Requests are coalesced into one pending callback and one pending scan, so an event storm cannot turn into a
        /// busy loop. This never bypasses a check: it only removes waiting time before the regular decision path.
        /// </summary>
        public void RequestImmediateTick()
        {
            if (_disposed) return;
            _metrics.CountEvent();
            long eventAt = Clock.NowMs;
            lock (_wakeGate)
            {
                if (_wakePending) return;
                _wakePending = true;
            }
            if (_invoker == null || _invoker.IsDisposed || !_invoker.IsHandleCreated)
            {
                lock (_wakeGate) { _wakePending = false; }
                return;
            }
            try
            {
                _invoker.BeginInvoke(new Action(delegate
                {
                    lock (_wakeGate) { _wakePending = false; }
                    if (_disposed) return;
                    if (_eventAtMs == Clock.Never) _eventAtMs = eventAt;
                    _immediatePending = true;
                    _immediateAtMs = eventAt;
                    Tick();
                }));
            }
            catch (Exception)
            {
                // A closed message loop only costs the fast path for this request; the timer still ticks.
                lock (_wakeGate) { _wakePending = false; }
            }
        }

        private void OnForegroundEvent()
        {
            RequestImmediateTick();
        }

        private void OnTabSelectionEvent()
        {
            RequestImmediateTick();
        }

        /// <summary>Called by the UI timer. Cheap when no supported browser is in front.</summary>
        public void Tick()
        {
            if (_disposed) return;
            long now = Clock.NowMs;

            IntPtr foreground = NativeMethods.GetForegroundRootWindow();
            bool foregroundChanged = foreground != _foreground;
            if (foregroundChanged)
            {
                _foreground = foreground;
                _hover.Reset();
                InvalidateConfirmation();
            }

            if (_closeInFlight && Clock.HasElapsed(_closeStartedMs, AppInfo.CloseValidationTimeoutMs))
            {
                _notice = StringId.NoticeValidationExpired;
                _closeInFlight = false;
                InvalidateConfirmation();
            }

            BrowserKind browser = ProcessIdentity.GetBrowser(foreground);
            bool supported = BrowserCatalog.IsSupported(browser);
            bool browserEnabled = supported && BrowserCatalog.IsEnabled(_settings.Browsers, browser);
            // The minimized wording is only correct when the minimized window really is a supported browser.
            bool minimized = supported && foreground != IntPtr.Zero && NativeMethods.IsIconic(foreground);
            bool available = browserEnabled && !minimized;
            if (_browserInForeground != available || _foregroundBrowser != browser)
            {
                _hover.Reset();
                InvalidateConfirmation();
            }
            _browserInForeground = available;
            _foregroundBrowser = browser;
            _foregroundBrowserKnown = browser != BrowserKind.None;
            _foregroundBrowserDisabled = supported && !browserEnabled;
            _ownWindowInForeground = foreground != IntPtr.Zero && ProcessIdentity.IsOwnWindow(foreground);
            if (supported) _lastBrowserWindow = foreground;

            if (!_enabled)
            {
                HideOverlay();
                _targetWindow = IntPtr.Zero;
                ClearHoverRegion();
                UpdateEventTarget(IntPtr.Zero);
                CheckStall();
                NotifyIfChanged();
                return;
            }

            if (!_browserInForeground)
            {
                HideOverlay();
                _targetWindow = IntPtr.Zero;
                ClearHoverRegion();
                _hover.Reset();
                UpdateEventTarget(IntPtr.Zero);
                _idleReason = IdleReasonFor(minimized);
                RequestDashboardScanIfVisible(now);
                CheckStall();
                NotifyIfChanged();
                return;
            }

            _targetWindow = foreground;
            _idleReason = StringId.None;
            UpdateEventTarget(foreground);
            // A window that just came to the front is a new thing to confirm: scan it now instead of waiting out the
            // interval that belonged to the previous window.
            if (foregroundChanged) _lastOverlayRequestMs = Clock.Never;

            // Hide when the confirmation is older than the stale window (for example, a stalled UIA call).
            if (_overlay.Visible && Clock.HasElapsed(_lastConfirmedMs, AppInfo.ScanStaleMs))
            {
                HideOverlay();
                _hover.Reset();
            }

            // The pointer gate is evaluated on every tick (it is what remembers where the pointer is), and the
            // interval that follows it is the effective one, so an arrival in the tab strip is served immediately.
            bool pointerAllowsScan = ShouldScanNow(foreground);
            bool immediate = _immediatePending && !Clock.HasElapsed(_immediateAtMs, AppInfo.ScanStaleMs);
            if (_immediatePending && !immediate) _immediatePending = false;
            int interval = immediate ? AppInfo.EventWatchMinScanGapMs : EffectiveScanIntervalMs();
            if (!_closeInFlight && !_overlayWorker.IsBusy && pointerAllowsScan
                && Clock.HasElapsed(_lastOverlayRequestMs, interval))
            {
                // The interval timestamp only advances when the worker really accepted the request, so a refused
                // request cannot silently cost a whole extra interval.
                if (Submit(_overlayWorker, ScanLane.Overlay, foreground, Point.Empty))
                {
                    _lastOverlayRequestMs = now;
                    if (immediate)
                    {
                        _immediatePending = false;
                        _metrics.CountEventScanRequest();
                    }
                }
            }

            if (_settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab) UpdateHoverVisibility(now);

            LogMetricsIfDue(now);
            CheckStall();
            NotifyIfChanged();
        }

        private StringId IdleReasonFor(bool minimized)
        {
            if (minimized) return StringId.IdleBrowserMinimized;
            if (_ownWindowInForeground) return StringId.IdleOwnWindow;
            if (_foregroundBrowserDisabled) return StringId.IdleBrowserDisabled;
            if (_foregroundBrowserKnown && !BrowserCatalog.IsSupported(_foregroundBrowser))
                return StringId.IdleBrowserUnsupported;
            return StringId.IdleBrowserNotForeground;
        }

        private void UpdateEventTarget(IntPtr window)
        {
            if (_tabWatcher == null) return;
            _tabWatcher.SetTarget(window);
        }

        /// <summary>
        /// Hover mode only: in always mode every tick scans, as before. In hover mode the pointer must be inside the
        /// window's tab strip (or the button must already be visible) before a scan is worth submitting. A pointer
        /// outside the strip cannot satisfy any eligibility rule, so this can only delay the button and can never
        /// authorize a close.
        ///
        /// The transition into the strip is the moment the user asks for the tab, so it forces the next scan to happen
        /// on this tick rather than after the rest of the configured interval. That is one scan per arrival, never a
        /// tighter loop: while the pointer stays inside, the ordinary cadence applies.
        /// </summary>
        private bool ShouldScanNow(IntPtr window)
        {
            if (_settings.VisibilityMode != OverlayVisibilityMode.HoverOnSelectedTab) return true;
            Rectangle windowBounds = NativeMethods.GetWindowBounds(window);
            if (windowBounds.IsEmpty) return true;
            Point pointer;
            if (!NativeMethods.TryGetPointerPosition(out pointer)) return true;
            Rectangle strip = TabGeometry.GetTabStripRegion(windowBounds, NativeMethods.GetWindowDpi(window));
            if (strip.IsEmpty) return true;
            bool inside = strip.Contains(pointer);
            if (inside && !_stripPointerInside)
            {
                // The user just asked for this tab with the pointer: serve the next scan immediately and start the
                // event-to-visible measurement from here, so the diagnostic latency covers hover mode too.
                _lastOverlayRequestMs = Clock.Never;
                if (_eventAtMs == Clock.Never) _eventAtMs = Clock.NowMs;
            }
            _stripPointerInside = inside;
            return _overlay.Visible || inside;
        }

        /// <summary>
        /// Scan interval for the current state and the active profile. While the pointer is inside the tab strip in
        /// hover mode the user is working with the tab in front of them, so the loop runs at the documented minimum
        /// instead of waiting out the full configured interval. No deadline, staleness window, budget or confirmation
        /// rule is affected.
        /// </summary>
        private int EffectiveScanIntervalMs()
        {
            if (_settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab && _stripPointerInside
                && _settings.ScanIntervalMs > AppInfo.MinScanIntervalMs) return AppInfo.MinScanIntervalMs;
            return _settings.ScanIntervalMs;
        }

        /// <summary>
        /// Hover mode decision. Eligibility mirrors the always-show rules exactly; the pointer position only decides
        /// whether the × is displayed right now, and the bounded grace period keeps it stable while the pointer moves
        /// from the tab onto the button.
        /// </summary>
        private void UpdateHoverVisibility(long now)
        {
            ScanResult result = _latest;
            bool eligible = _enabled
                && _browserInForeground
                && !_stalled
                && !_closeInFlight
                && Clock.HasElapsed(_lastSentMs, AppInfo.CloseCooldownMs)
                && !Clock.HasElapsed(_lastConfirmedMs, AppInfo.ScanStaleMs)
                && result != null
                && result.Outcome == ScanOutcome.Ready
                && result.SelectedHorizontalTab != null;

            Point pointer;
            bool hasPointer = NativeMethods.TryGetPointerPosition(out pointer);
            bool inside = hasPointer && HoverRegion.Contains(_hoverRegion, pointer);

            if (_hover.Update(eligible, inside, now, _settings.HoverGraceMs))
            {
                if (result != null) ShowOverlayFor(result);
                return;
            }
            if (_overlay.Visible) HideOverlay();
        }

        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;
            _settings.OverlayEnabled = enabled;
            _closeInFlight = false;
            _notice = StringId.None;
            InvalidateConfirmation();
            if (!enabled) _latest = null;
            AppLog.Info(enabled ? "Overlay enabled by user." : "Overlay disabled by user.");
            ForceNotify();
        }

        public void SetVisibilityMode(OverlayVisibilityMode mode)
        {
            if (mode != OverlayVisibilityMode.HoverOnSelectedTab) mode = OverlayVisibilityMode.AlwaysWhenSafe;
            if (_settings.VisibilityMode == mode) return;
            _settings.VisibilityMode = mode;
            // The next arrival in the tab strip is a new arrival, whatever the pointer happens to be doing now.
            _stripPointerInside = false;
            _notice = StringId.None;
            InvalidateConfirmation();
            AppLog.Info("Visibility mode set to " + AppSettings.VisibilityToken(mode) + ".");
            ForceNotify();
        }

        /// <summary>
        /// Applies a new × size and inset. In-flight close validations are discarded, because the click area that was
        /// validated no longer matches the button on screen.
        /// </summary>
        public void SetButtonGeometry(int sizeLogical, int insetLogical)
        {
            int size = TabGeometry.ClampButtonSize(sizeLogical);
            int inset = TabGeometry.ClampButtonInset(insetLogical);
            if (_settings.ButtonSizeLogical == size && _settings.ButtonInsetLogical == inset) return;
            _settings.ButtonSizeLogical = size;
            _settings.ButtonInsetLogical = inset;
            InvalidateConfirmation();
            ForceNotify();
        }

        public void SetHoverGraceMs(int milliseconds)
        {
            if (milliseconds < AppInfo.MinHoverGraceMs) milliseconds = AppInfo.MinHoverGraceMs;
            if (milliseconds > AppInfo.MaxHoverGraceMs) milliseconds = AppInfo.MaxHoverGraceMs;
            _settings.HoverGraceMs = milliseconds;
        }

        public void SetScanIntervalMs(int milliseconds)
        {
            if (milliseconds < AppInfo.MinScanIntervalMs) milliseconds = AppInfo.MinScanIntervalMs;
            if (milliseconds > AppInfo.MaxScanIntervalMs) milliseconds = AppInfo.MaxScanIntervalMs;
            _settings.ScanIntervalMs = milliseconds;
        }

        /// <summary>Applies the browser selection. A change invalidates any confirmation from the previous set.</summary>
        public void SetBrowsers(int browserMask)
        {
            int mask = browserMask & BrowserCatalog.SupportedMask();
            if (mask == BrowserCatalog.NoBrowsers)
            {
                // Refuse rather than choose for the user: an empty selection would make the × unable to appear at all,
                // and silently re-enabling a browser the user just cleared would be worse.
                AppLog.Warn("No supported browser is enabled; the previous browser selection is kept.");
                return;
            }
            if (_settings.Browsers == mask) return;
            _settings.Browsers = mask;
            InvalidateConfirmation();
            AppLog.Info("Enabled browsers: " + BrowserCatalog.MaskTokenList(mask) + ".");
            ForceNotify();
        }

        public void SetProfile(PerformanceProfile profile)
        {
            if (_settings.Profile == profile) return;
            _settings.Profile = profile;
            // The new cadence applies to the next decision, and an immediate tick makes that visible at once.
            AppLog.Info("Performance profile set to " + Profiles.Token(profile) + ".");
            RequestImmediateTick();
            ForceNotify();
        }

        /// <summary>
        /// Pushes the whole settings set into the controller. Every changed value is applied immediately, and a
        /// geometry, mode, appearance or browser change invalidates any pending validation.
        /// </summary>
        public void ApplySettings(AppSettings settings)
        {
            if (settings == null) return;
            settings.Normalize();
            SetHoverGraceMs(settings.HoverGraceMs);
            SetScanIntervalMs(settings.ScanIntervalMs);
            SetButtonGeometry(settings.ButtonSizeLogical, settings.ButtonInsetLogical);
            SetVisibilityMode(settings.VisibilityMode);
            SetBrowsers(settings.Browsers);
            SetProfile(settings.Profile);
            bool appearanceChanged = _overlay.ApplyAppearance(settings);
            _settings.ButtonColor = settings.ButtonColor;
            _settings.ButtonBorder = settings.ButtonBorder;
            _settings.ButtonOpacityPercent = settings.ButtonOpacityPercent;
            _settings.Language = settings.Language;
            if (appearanceChanged) InvalidateConfirmation();
            ForceNotify();
            SetEnabled(settings.OverlayEnabled);
        }

        public void SetDashboardVisible(bool visible)
        {
            _dashboardVisible = visible;
        }

        public ActiveTabSnapshot GetSnapshot()
        {
            long now = Clock.NowMs;
            ActiveTabSnapshot snapshot = new ActiveTabSnapshot();
            snapshot.Enabled = _enabled;
            snapshot.OverlayVisible = _overlay.Visible;
            snapshot.SupportedBrowserInForeground = _browserInForeground;
            snapshot.Browser = _foregroundBrowser;
            snapshot.ForegroundBrowserDisabled = _foregroundBrowserDisabled;
            snapshot.BrowserUnsupported = _foregroundBrowserKnown && !BrowserCatalog.IsSupported(_foregroundBrowser);
            snapshot.OwnWindowInForeground = _ownWindowInForeground;
            snapshot.Stalled = _stalled;
            snapshot.VisibilityMode = _settings.VisibilityMode;
            snapshot.Profile = _settings.Profile;
            snapshot.IdleReason = GetIdleReason();
            snapshot.Notice = Strings.Get(_notice);
            snapshot.Scan = _latest;
            snapshot.HiddenReason = GetHiddenReason();
            snapshot.ConfirmationAgeMs = _lastConfirmedMs == Clock.Never ? -1 : now - _lastConfirmedMs;
            snapshot.LastScanAgeMs = _lastScanCompletedMs == Clock.Never ? -1 : now - _lastScanCompletedMs;
            snapshot.ScanStale = _lastScanCompletedMs == Clock.Never || Clock.HasElapsed(_lastScanCompletedMs, AppInfo.ScanStaleMs);
            snapshot.Metrics = _metrics.Snapshot();
            snapshot.OverlayWorker = Describe(_overlayWorker);
            snapshot.CloseWorker = Describe(_closeWorker);
            snapshot.DashboardWorker = Describe(_dashboardWorker);
            snapshot.Watcher = _tabWatcher == null ? null : _tabWatcher.Snapshot();
            snapshot.EffectiveSettings = SettingsSummary.Build(_settings);
            snapshot.Settings = _settings.Copy();
            return snapshot;
        }

        private static WorkerSnapshot Describe(ScanWorker worker)
        {
            WorkerSnapshot snapshot = new WorkerSnapshot();
            snapshot.Busy = worker.IsBusy;
            snapshot.RunningForMs = worker.RunningForMs();
            return snapshot;
        }

        private string GetIdleReason()
        {
            return _idleReason == StringId.None ? string.Empty : Strings.Get(_idleReason);
        }

        private string GetHiddenReason()
        {
            if (_overlay.Visible) return string.Empty;
            if (!_enabled) return Strings.Get(StringId.HiddenOverlayDisabled);
            if (_stalled) return Strings.Get(StringId.HiddenStalled);
            if (!_browserInForeground) return GetIdleReason();
            if (_closeInFlight) return Strings.Get(StringId.HiddenCloseInFlight);
            if (!Clock.HasElapsed(_lastSentMs, AppInfo.CloseCooldownMs)) return Strings.Get(StringId.HiddenCooldown);
            if (!string.IsNullOrEmpty(_overlayFailure)) return Strings.Get(StringId.OverlayShowFailed, _overlayFailure);
            if (_settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab && !_hover.Visible
                && (_hoverRegion.IsEmpty || !_hover.PointerInside)) return Strings.Get(StringId.HiddenHoverOutside);
            if (_latest == null) return Strings.Get(StringId.HiddenWaitingScan);
            if (_latest.Outcome != ScanOutcome.Ready) return _latest.Message;
            return Strings.Get(StringId.HiddenStaleConfirmation);
        }

        private void RequestDashboardScanIfVisible(long now)
        {
            if (!_dashboardVisible || _lastBrowserWindow == IntPtr.Zero) return;
            if (!NativeMethods.IsWindow(_lastBrowserWindow) || !BrowserCatalog.IsSupported(ProcessIdentity.GetBrowser(_lastBrowserWindow)))
                return;
            ProfileCadence cadence = Profiles.CadenceFor(_settings.Profile);
            if (_dashboardWorker.IsBusy || !Clock.HasElapsed(_lastDashboardRequestMs, cadence.DashboardScanIntervalMs)) return;

            if (Submit(_dashboardWorker, ScanLane.Dashboard, _lastBrowserWindow, Point.Empty)) _lastDashboardRequestMs = now;
        }

        /// <summary>Submits a request and reports whether the worker accepted it (a lane holds one request at a time).</summary>
        private bool Submit(ScanWorker worker, ScanLane lane, IntPtr window, Point clickScreen)
        {
            ScanRequest request = new ScanRequest(++_nextRequestId, lane, _epoch, window, clickScreen);
            if (worker.TrySubmit(request))
            {
                _metrics.CountRequest();
                return true;
            }
            _metrics.CountRefused();
            return false;
        }

        /// <summary>Marshals a worker result to the UI thread. Never blocks the worker.</summary>
        private void Post(ScanCompletion completion)
        {
            if (_disposed || _invoker.IsDisposed || !_invoker.IsHandleCreated) return;
            _invoker.BeginInvoke(new Action(delegate { OnScanCompleted(completion); }));
        }

        private void OnScanCompleted(ScanCompletion completion)
        {
            if (_disposed) return;
            ScanRequest request = completion.Request;
            long delivered = Clock.NowMs;
            {
                long scanMs = completion.Result == null ? 0 : completion.Result.ElapsedMs;
                long queued = request.WorkerStartMs == Clock.Never ? 0 : request.WorkerStartMs - request.StartedMs;
                _metrics.Record(request.Lane, queued, scanMs, delivered - request.StartedMs);
                if (completion.Result != null && completion.Result.Outcome == ScanOutcome.Error) _metrics.CountError();
            }
            if (request.Lane == ScanLane.Close)
            {
                OnCloseValidated(completion);
                return;
            }

            // A result from an older state (different window, enable toggle, browser selection or keystroke) must not
            // touch the overlay.
            if (request.Epoch != _epoch)
            {
                _metrics.CountDiscarded();
                return;
            }
            if (!ScanSafety.IsFresh(request.StartedMs, delivered, AppInfo.ScanStaleMs))
            {
                _metrics.CountStale();
                InvalidateConfirmation();
                // Preserve diagnostics, never authorization, even when a slow provider finally returns an error.
                _latest = completion.Result;
                LogOutcomeIfChanged(completion.Result);
                ForceNotify();
                return;
            }

            if (request.Lane == ScanLane.Dashboard)
            {
                _latest = completion.Result;
                _lastScanCompletedMs = delivered;
                LogOutcomeIfChanged(completion.Result);
                ForceNotify();
                return;
            }

            if (!_enabled || !_browserInForeground || _closeInFlight || request.Window != _targetWindow) return;
            if (NativeMethods.GetForegroundRootWindow() != request.Window || NativeMethods.IsIconic(request.Window)
                || !BrowserCatalog.IsSupported(ProcessIdentity.GetBrowser(request.Window)))
            {
                InvalidateConfirmation();
                ForceNotify();
                return;
            }

            ScanResult result = completion.Result;
            _latest = result;
            _lastScanCompletedMs = delivered;
            bool coolingDown = !Clock.HasElapsed(_lastSentMs, AppInfo.CloseCooldownMs);
            if (result.Outcome == ScanOutcome.Ready && !coolingDown && !_stalled)
            {
                if (result.WindowBounds != NativeMethods.GetWindowBounds(request.Window)
                    || result.Dpi != NativeMethods.GetWindowDpi(request.Window))
                {
                    InvalidateConfirmation();
                }
                else
                {
                    _lastConfirmedMs = request.StartedMs;
                    UpdateHoverRegion(result);
                    // In hover mode showing is the pointer's decision; in always mode the confirmed scan shows it.
                    if (_settings.VisibilityMode == OverlayVisibilityMode.AlwaysWhenSafe) ShowOverlayFor(result);
                    else UpdateHoverVisibility(Clock.NowMs);
                }
            }
            else
            {
                HideOverlay();
                ClearHoverRegion();
            }

            LogOutcomeIfChanged(result);
            ForceNotify();
        }

        private void ShowOverlayFor(ScanResult result)
        {
            if (result == null || result.SelectedHorizontalTab == null
                || result.SelectedHorizontalTab.RuntimeId == null || result.SelectedHorizontalTab.RuntimeId.Length == 0)
            {
                HideOverlay();
                return;
            }
            Rectangle target = TabGeometry.GetOverlayBounds(result.SelectedHorizontalTab.Bounds, result.Dpi,
                _settings.ButtonSizeLogical, _settings.ButtonInsetLogical);
            if (target.IsEmpty)
            {
                HideOverlay();
                return;
            }
            if (!TabInfo.SameIdentity(_displayedTabRuntimeId, result.SelectedHorizontalTab.RuntimeId))
                _overlay.CancelPendingClick();
            string failure;
            if (!_overlay.ShowAt(target, out failure))
            {
                _overlayFailure = failure;
                AppLog.WarnThrottled("overlay-show", failure);
                _placedVisible = false;
                return;
            }
            _displayedTabRuntimeId = (int[])result.SelectedHorizontalTab.RuntimeId.Clone();
            _overlayFailure = string.Empty;
            // One log line per real placement change: counts and timings only, never a title or a URL. This is what
            // lets an external measurement compare its own clock with the moment the × became visible.
            if (!_placedVisible || _placedTarget != target)
            {
                bool moved = _placedVisible;
                _placedVisible = true;
                _placedTarget = target;
                long eventMs = _eventAtMs == Clock.Never ? -1 : Clock.NowMs - _eventAtMs;
                if (eventMs >= 0)
                {
                    _metrics.RecordEventToVisible(eventMs);
                    _eventAtMs = Clock.Never;
                }
                AppLog.Info("Overlay placement: " + (moved ? "moved" : "visible") + " (scanMs=" + result.ElapsedMs
                    + (eventMs < 0 ? string.Empty : ", eventMs=" + eventMs) + ").");
            }
        }

        /// <summary>
        /// Recomputes the hover region from a confirmed scan. A geometry change clears hover state, so a hover that
        /// belonged to the previous tab, window position or button size cannot keep the button alive.
        /// </summary>
        private void UpdateHoverRegion(ScanResult result)
        {
            Rectangle tab = result.SelectedHorizontalTab == null ? Rectangle.Empty : result.SelectedHorizontalTab.Bounds;
            Rectangle overlayBounds = tab.IsEmpty
                ? Rectangle.Empty
                : TabGeometry.GetOverlayBounds(tab, result.Dpi, _settings.ButtonSizeLogical, _settings.ButtonInsetLogical);
            Rectangle region = HoverRegion.ForSelectedTab(tab, overlayBounds);
            if (region != _hoverRegion)
            {
                _hoverRegion = region;
                _hover.Reset();
            }
        }

        private void ClearHoverRegion()
        {
            if (!_hoverRegion.IsEmpty) _hoverRegion = Rectangle.Empty;
            _hover.Reset();
        }

        private void OnOverlayClicked(Point screenPoint)
        {
            if (_disposed || !_enabled) return;

            // Capture the displayed target before invalidation. A new selected tab at the same coordinates is NOT
            // the target the user clicked. Missing identity refuses; no title-based fallback is permitted.
            int[] expected = _displayedTabRuntimeId == null ? null : (int[])_displayedTabRuntimeId.Clone();
            bool wasVisible = _overlay.Visible;
            // Hide first: the button must not stay clickable while its validation is pending.
            HideOverlay();
            _hover.Reset();
            if (_closeInFlight || !wasVisible || expected == null || expected.Length == 0
                || KeyboardInjector.BlockingModifierDown()) return;

            if (!Clock.HasElapsed(_lastSentMs, AppInfo.CloseCooldownMs))
            {
                AppLog.Info("Click ignored during the close cooldown.");
                return;
            }

            IntPtr foreground = NativeMethods.GetForegroundRootWindow();
            if (foreground == IntPtr.Zero || foreground != _targetWindow
                || !BrowserCatalog.IsSupported(ProcessIdentity.GetBrowser(foreground, true)))
            {
                AppLog.Info("Click ignored: the target browser is no longer the foreground window.");
                _notice = StringId.NoticeBrowserNotForeground;
                ForceNotify();
                return;
            }

            InvalidateConfirmation();
            ScanRequest request = new ScanRequest(++_nextRequestId, ScanLane.Close, _epoch, foreground, screenPoint);
            request.ExpectedTabRuntimeId = expected;
            if (!_closeWorker.TrySubmit(request))
            {
                _metrics.CountRefused();
                AppLog.Warn("Close validation lane was busy; the click was ignored.");
                _notice = StringId.NoticeCloseLaneBusy;
                ForceNotify();
                return;
            }
            _metrics.CountRequest();
            _closeRequestId = request.Id;
            _closeInFlight = true;
            _closeStartedMs = Clock.NowMs;
            _notice = StringId.NoticeValidating;
            ForceNotify();
        }

        private void OnCloseValidated(ScanCompletion completion)
        {
            ScanRequest request = completion.Request;
            if (!_closeInFlight || request.Id != _closeRequestId) return;
            _closeInFlight = false;
            ScanResult scan = completion.Result;

            if (request.Epoch != _epoch)
            {
                AppLog.Info("Close click cancelled: window state changed during validation.");
                _notice = StringId.NoticeEpochChanged;
                ForceNotify();
                return;
            }

            IntPtr foreground = NativeMethods.GetForegroundRootWindow();
            CloseRequest check = new CloseRequest();
            check.OverlayEnabled = _enabled;
            check.EpochCurrent = true;
            check.ScannedWindow = request.Window;
            check.ForegroundNow = foreground;
            // Fresh query: the decision must not rest on a cached identity.
            BrowserKind foregroundBrowser = ProcessIdentity.GetBrowser(foreground, true);
            check.ForegroundIsSupportedBrowser = BrowserCatalog.IsSupported(foregroundBrowser)
                && BrowserCatalog.IsEnabled(_settings.Browsers, foregroundBrowser);
            check.Minimized = foreground != IntPtr.Zero && NativeMethods.IsIconic(foreground);
            check.BlockingModifierDown = KeyboardInjector.BlockingModifierDown();
            check.Scan = scan;
            check.ExpectedTabRuntimeId = request.ExpectedTabRuntimeId;
            check.Click = request.ClickScreen;
            check.NowMs = Clock.NowMs;
            check.StartedMs = request.StartedMs;
            check.LastSentMs = _lastSentMs;
            check.OverlaySizeLogical = _settings.ButtonSizeLogical;
            check.OverlayInsetLogical = _settings.ButtonInsetLogical;

            if (scan.WindowBounds != NativeMethods.GetWindowBounds(request.Window)
                || scan.Dpi != NativeMethods.GetWindowDpi(request.Window)) check.EpochCurrent = false;
            CloseDecision decision = CloseGuard.Evaluate(check);
            if (!decision.Allowed)
            {
                AppLog.Info("Close click ignored: " + decision.Reason + " (scan outcome " + scan.Outcome + ").");
                _notice = StringId.NoticeClickRejected;
                _latest = scan;
                ForceNotify();
                return;
            }

            string failure;
            // Re-read immediately before injection; never rely only on the last timer tick.
            if (NativeMethods.GetForegroundRootWindow() != request.Window || KeyboardInjector.BlockingModifierDown()
                || !ScanSafety.IsFresh(request.StartedMs, Clock.NowMs, AppInfo.CloseValidationTimeoutMs))
            {
                InvalidateConfirmation();
                _notice = StringId.NoticeWindowOrModifierChanged;
                ForceNotify();
                return;
            }
            bool sent = KeyboardInjector.SendControlW(out failure);
            _lastSentMs = check.NowMs;
            InvalidateConfirmation();
            _latest = scan;

            if (sent)
            {
                AppLog.Info("Ctrl+W sent to the confirmed active tab; close cooldown started.");
                _notice = StringId.NoticeClosed;
            }
            else
            {
                AppLog.Error("Ctrl+W was not delivered: " + failure);
                _notice = StringId.NoticeCloseFailed;
                Action<string> failed = CloseFailed;
                if (failed != null) failed(Strings.Get(_notice));
            }
            ForceNotify();
        }

        /// <summary>Any event that makes pending results or the visible overlay untrustworthy. Hides the overlay.</summary>
        private void InvalidateConfirmation()
        {
            _epoch++;
            if (_closeInFlight) _notice = StringId.NoticeValidationCancelled;
            _closeInFlight = false;
            _closeRequestId = 0;
            _latest = null;
            _overlayFailure = string.Empty;
            _lastConfirmedMs = Clock.Never;
            _placedTarget = Rectangle.Empty;
            _placedVisible = false;
            ClearHoverRegion();
            HideOverlay();
        }

        private void HideOverlay()
        {
            _displayedTabRuntimeId = null;
            if (_overlay.Visible)
            {
                _overlay.Hide();
                if (_placedVisible) AppLog.Info("Overlay placement: hidden.");
                _placedVisible = false;
                _placedTarget = Rectangle.Empty;
            }
        }

        private void CheckStall()
        {
            long running = Math.Max(_overlayWorker.RunningForMs(), Math.Max(_closeWorker.RunningForMs(), _dashboardWorker.RunningForMs()));
            _stalled = running > AppInfo.ScanStallMs;
            if (_stalled)
            {
                HideOverlay();
                _hover.Reset();
                if (!_stallLogged)
                {
                    _stallLogged = true;
                    _metrics.CountStall();
                    AppLog.Warn("UI Automation has not returned for more than " + AppInfo.ScanStallMs
                        + " ms; the overlay stays hidden until the browser responds.");
                }
            }
            else if (_stallLogged && running == 0)
            {
                _stallLogged = false;
                AppLog.Info("UI Automation responded again.");
            }
        }

        /// <summary>At most one statistics line per interval, so long sessions cannot grow the log.</summary>
        private void LogMetricsIfDue(long now)
        {
            long completed = _metrics.CompletedCount;
            if (completed == 0) return;
            bool due = Clock.HasElapsed(_lastMetricsLogMs, AppInfo.MetricsLogIntervalMs)
                || completed - _lastMetricsLogCompleted >= AppInfo.MetricsLogScanCount;
            if (!due) return;
            _lastMetricsLogMs = now;
            _lastMetricsLogCompleted = completed;
            // Counters and timings only: no title, no URL, no provider text.
            AppLog.Info("Scan metrics: " + _metrics.Snapshot().Describe() + ".");
        }

        private void LogOutcomeIfChanged(ScanResult result)
        {
            if (result == null) return;
            if (result.Outcome == _loggedOutcome && result.Error == _loggedError) return;
            _loggedError = result.Error;
            _loggedOutcome = result.Outcome;
            string detail = result.Error == null ? string.Empty : " error=" + result.Error;
            // Counts, timings and outcome only: never a tab title, a window title or a URL.
            AppLog.Info("Overlay state: " + result.Outcome + " (key=" + result.Key + ", tabs=" + result.Tabs.Count
                + ", nodes=" + result.NodesVisited + ", ms=" + result.ElapsedMs + ")" + detail + ".");
        }

        /// <summary>True when the observable state differs from the state that was last pushed to the views.</summary>
        private bool HasUnnotifiedChange()
        {
            return _notifiedForeground != _foreground
                || _notifiedBrowser != _browserInForeground
                || _notifiedStalled != _stalled
                || _notifiedEnabled != _enabled
                || _notifiedCloseInFlight != _closeInFlight
                || _notifiedVisible != _overlay.Visible
                || _notifiedIdle != _idleReason
                || _notifiedNotice != _notice
                || !object.ReferenceEquals(_notifiedLatest, _latest)
                || !string.Equals(_notifiedOverlayFailure, _overlayFailure, StringComparison.Ordinal);
        }

        private void RememberNotifiedState()
        {
            _notifiedForeground = _foreground;
            _notifiedBrowser = _browserInForeground;
            _notifiedStalled = _stalled;
            _notifiedEnabled = _enabled;
            _notifiedCloseInFlight = _closeInFlight;
            _notifiedVisible = _overlay.Visible;
            _notifiedIdle = _idleReason;
            _notifiedNotice = _notice;
            _notifiedLatest = _latest;
            _notifiedOverlayFailure = _overlayFailure;
        }

        /// <summary>Pushes the current state to the views, but only when something observable changed.</summary>
        private void NotifyIfChanged()
        {
            if (!HasUnnotifiedChange()) return;
            RememberNotifiedState();
            NotifyChanged();
        }

        /// <summary>Notifies the views unconditionally. Used after a language change or a settings change.</summary>
        public void ForceNotify()
        {
            RememberNotifiedState();
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _overlay.Clicked -= OnOverlayClicked;
            if (_foregroundWatcher != null)
            {
                _foregroundWatcher.Changed -= OnForegroundEvent;
                _foregroundWatcher.Dispose();
            }
            if (_tabWatcher != null) _tabWatcher.Dispose();
            _overlayWorker.Dispose();
            _closeWorker.Dispose();
            _dashboardWorker.Dispose();
        }
    }
}
