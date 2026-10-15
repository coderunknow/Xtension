using System;
using System.Drawing;
using System.Windows.Forms;

namespace Xtension
{
    /// <summary>Read-only view of the controller for the dashboard and diagnostics. Snapshots are cheap value copies.</summary>
    internal sealed class ActiveTabSnapshot
    {
        public bool Enabled;
        public bool OverlayVisible;
        public bool BraveInForeground;
        public bool Stalled;
        public OverlayVisibilityMode VisibilityMode;
        public string IdleReason = string.Empty;
        public string Notice = string.Empty;
        public string HiddenReason = string.Empty;
        /// <summary>Latest accepted scan. Null before the first scan or while the overlay is disabled.</summary>
        public ScanResult Scan;
    }

    /// <summary>
    /// Owns the overlay state machine. All state is touched only on the UI thread. UI Automation runs on two worker
    /// threads: one for periodic overlay scans and one for close validation, so a × click never waits behind a scan.
    ///
    /// Safety model (unchanged by dev3):
    /// - The overlay is shown only after a Ready scan of the current foreground Brave window.
    /// - Every state change bumps an epoch. Results from an older epoch are discarded.
    /// - A click sends Ctrl+W only if a fresh scan confirms the click landed on the × of the one selected horizontal tab.
    /// - After a keystroke, the overlay is hidden and clicks are ignored for CloseCooldownMs.
    ///
    /// dev3 additions:
    /// - Visibility mode (always when safe, or only while the pointer is over the confirmed selected tab).
    /// - Configurable × size and inset, applied to painting, placement, hit testing and close validation together.
    /// - Change-gated view notification: a timer tick that changes nothing observable no longer rebuilds a snapshot
    ///   or re-renders the windows.
    /// - Hover mode skips UI Automation work while the pointer cannot be inside the horizontal tab strip. That can
    ///   only delay showing the button; it never authorizes anything.
    /// </summary>
    internal sealed class ActiveTabController : IDisposable
    {
        private readonly CloseButtonOverlay _overlay;
        private readonly Control _invoker;
        private readonly ScanWorker _overlayWorker;
        private readonly ScanWorker _closeWorker;
        private readonly HoverTracker _hover = new HoverTracker();
        private readonly AppSettings _settings;

        private volatile bool _disposed;
        private bool _enabled;
        private bool _dashboardVisible;
        private bool _braveInForeground;
        private bool _stalled;
        private bool _stallLogged;
        private long _epoch;
        private long _nextRequestId;
        private IntPtr _foreground = IntPtr.Zero;
        private IntPtr _targetWindow = IntPtr.Zero;
        private IntPtr _lastBraveWindow = IntPtr.Zero;
        private StringId _idleReason = StringId.IdleStarting;
        private StringId _notice = StringId.None;
        private ScanResult _latest;
        private ScanOutcome _loggedOutcome = ScanOutcome.Idle;
        private string _loggedError;
        private long _lastOverlayRequestMs = Clock.Never;
        private long _lastDashboardRequestMs = Clock.Never;
        private long _lastConfirmedMs = Clock.Never;
        private long _lastSentMs = Clock.Never;
        private bool _closeInFlight;
        private long _closeRequestId;
        private long _closeStartedMs = Clock.Never;
        private string _overlayFailure = string.Empty;
        private Rectangle _hoverRegion = Rectangle.Empty;
        /// <summary>Hover mode: was the pointer inside the tab strip on the previous tick? Used to scan the moment the
        /// pointer arrives instead of waiting out an interval that belongs to the previous state.</summary>
        private bool _stripPointerInside;

        // Last state that was pushed to the views, so an uneventful tick costs nothing.
        private IntPtr _notifiedForeground = new IntPtr(-1);
        private bool _notifiedBrave;
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
        {
            _overlay = overlay;
            _invoker = invoker;
            _settings = settings == null ? AppSettings.Defaults() : settings;
            _settings.Normalize();
            _enabled = _settings.OverlayEnabled;
            _overlayWorker = new ScanWorker("Xtension UIA overlay scan", Post, ScanOverlay);
            _closeWorker = new ScanWorker("Xtension UIA close validation", Post, ScanOverlay);
            _overlay.Clicked += OnOverlayClicked;
        }

        /// <summary>Scan delegate that carries the configured × geometry into every scan decision.</summary>
        private ScanResult ScanOverlay(IntPtr window)
        {
            return UiaTabScanner.Scan(window, _settings.ButtonSizeLogical, _settings.ButtonInsetLogical);
        }

        /// <summary>Current effective settings copy. Callers must go through the Set* methods to apply changes.</summary>
        public AppSettings Settings
        {
            get { return _settings; }
        }

        /// <summary>Called by the UI timer. Cheap when Brave is not in front.</summary>
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

            bool isBrave = ProcessIdentity.IsBraveWindow(foreground);
            // The minimized wording is only correct when the minimized window really is Brave (dev2 follow-up).
            bool minimized = isBrave && foreground != IntPtr.Zero && NativeMethods.IsIconic(foreground);
            bool available = isBrave && !minimized;
            if (_braveInForeground != available)
            {
                _hover.Reset();
                InvalidateConfirmation();
            }
            _braveInForeground = available;
            if (isBrave) _lastBraveWindow = foreground;

            if (!_enabled)
            {
                HideOverlay();
                _targetWindow = IntPtr.Zero;
                ClearHoverRegion();
                CheckStall();
                NotifyIfChanged();
                return;
            }

            if (!_braveInForeground)
            {
                HideOverlay();
                _targetWindow = IntPtr.Zero;
                ClearHoverRegion();
                _hover.Reset();
                _idleReason = minimized ? StringId.IdleBraveMinimized : StringId.IdleBraveNotForeground;
                RequestDashboardScanIfVisible(now);
                CheckStall();
                NotifyIfChanged();
                return;
            }

            _targetWindow = foreground;
            _idleReason = StringId.None;
            // A window that just came to the front is a new thing to confirm: scan it now instead of waiting out the
            // interval that belonged to the previous window. One extra scan per focus change, cadence unchanged.
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
            if (!_closeInFlight && !_overlayWorker.IsBusy && pointerAllowsScan
                && Clock.HasElapsed(_lastOverlayRequestMs, EffectiveScanIntervalMs()))
            {
                _lastOverlayRequestMs = now;
                Submit(_overlayWorker, ScanKind.Overlay, foreground, Point.Empty);
            }

            if (_settings.VisibilityMode == OverlayVisibilityMode.HoverOnSelectedTab) UpdateHoverVisibility(now);

            CheckStall();
            NotifyIfChanged();
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
            if (inside && !_stripPointerInside) _lastOverlayRequestMs = Clock.Never;
            _stripPointerInside = inside;
            return _overlay.Visible || inside;
        }

        /// <summary>
        /// Scan interval for the current state. While the pointer is inside the tab strip in hover mode the user is
        /// working with the tab in front of them, so the loop runs at the documented minimum instead of waiting out
        /// the full configured interval; outside the strip the configured interval applies unchanged. No deadline,
        /// staleness window, budget or confirmation rule is affected.
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
                && _braveInForeground
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

        /// <summary>
        /// Pushes the whole settings set into the controller. Every changed value is applied immediately, and a
        /// geometry or mode change invalidates any pending validation.
        /// </summary>
        public void ApplySettings(AppSettings settings)
        {
            if (settings == null) return;
            settings.Normalize();
            SetHoverGraceMs(settings.HoverGraceMs);
            SetScanIntervalMs(settings.ScanIntervalMs);
            SetButtonGeometry(settings.ButtonSizeLogical, settings.ButtonInsetLogical);
            SetVisibilityMode(settings.VisibilityMode);
            SetEnabled(settings.OverlayEnabled);
        }

        public void SetDashboardVisible(bool visible)
        {
            _dashboardVisible = visible;
        }

        public ActiveTabSnapshot GetSnapshot()
        {
            ActiveTabSnapshot snapshot = new ActiveTabSnapshot();
            snapshot.Enabled = _enabled;
            snapshot.OverlayVisible = _overlay.Visible;
            snapshot.BraveInForeground = _braveInForeground;
            snapshot.Stalled = _stalled;
            snapshot.VisibilityMode = _settings.VisibilityMode;
            snapshot.IdleReason = GetIdleReason();
            snapshot.Notice = Strings.Get(_notice);
            snapshot.Scan = _latest;
            snapshot.HiddenReason = GetHiddenReason();
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
            if (!_braveInForeground) return GetIdleReason();
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
            if (!_dashboardVisible || _lastBraveWindow == IntPtr.Zero) return;
            if (!NativeMethods.IsWindow(_lastBraveWindow) || !ProcessIdentity.IsBraveWindow(_lastBraveWindow)) return;
            if (_overlayWorker.IsBusy || !Clock.HasElapsed(_lastDashboardRequestMs, AppInfo.DashboardScanIntervalMs)) return;

            _lastDashboardRequestMs = now;
            Submit(_overlayWorker, ScanKind.Dashboard, _lastBraveWindow, Point.Empty);
        }

        private void Submit(ScanWorker worker, ScanKind kind, IntPtr window, Point clickScreen)
        {
            ScanRequest request = new ScanRequest(++_nextRequestId, kind, _epoch, window, clickScreen);
            worker.TrySubmit(request);
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
            if (request.Kind == ScanKind.Close)
            {
                OnCloseValidated(completion);
                return;
            }

            // A result from an older state (different window, enable toggle, or keystroke) must not touch the overlay.
            if (request.Epoch != _epoch) return;
            if (!ScanSafety.IsFresh(request.StartedMs, Clock.NowMs, AppInfo.ScanStaleMs))
            {
                InvalidateConfirmation();
                // Preserve diagnostics, never authorization, even when a slow provider finally returns an error.
                _latest = completion.Result;
                LogOutcomeIfChanged(completion.Result);
                ForceNotify();
                return;
            }

            if (request.Kind == ScanKind.Dashboard)
            {
                _latest = completion.Result;
                LogOutcomeIfChanged(completion.Result);
                ForceNotify();
                return;
            }

            if (!_enabled || !_braveInForeground || _closeInFlight || request.Window != _targetWindow) return;
            if (NativeMethods.GetForegroundRootWindow() != request.Window || NativeMethods.IsIconic(request.Window)
                || !ProcessIdentity.IsBraveWindow(request.Window))
            {
                InvalidateConfirmation();
                ForceNotify();
                return;
            }

            ScanResult result = completion.Result;
            _latest = result;
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
            if (result == null || result.SelectedHorizontalTab == null)
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
            string failure;
            if (!_overlay.ShowAt(target, out failure))
            {
                _overlayFailure = failure;
                AppLog.WarnThrottled("overlay-show", failure);
            }
            else _overlayFailure = string.Empty;
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

            // Hide first: the button must not stay clickable while its validation is pending.
            HideOverlay();
            _hover.Reset();
            if (_closeInFlight) return;

            if (!Clock.HasElapsed(_lastSentMs, AppInfo.CloseCooldownMs))
            {
                AppLog.Info("Click ignored during the close cooldown.");
                return;
            }

            IntPtr foreground = NativeMethods.GetForegroundRootWindow();
            if (foreground == IntPtr.Zero || foreground != _targetWindow || !ProcessIdentity.IsBraveWindow(foreground))
            {
                AppLog.Info("Click ignored: Brave is no longer the foreground window.");
                _notice = StringId.NoticeBraveNotForeground;
                ForceNotify();
                return;
            }

            InvalidateConfirmation();
            ScanRequest request = new ScanRequest(++_nextRequestId, ScanKind.Close, _epoch, foreground, screenPoint);
            if (!_closeWorker.TrySubmit(request))
            {
                AppLog.Warn("Close validation lane was busy; the click was ignored.");
                _notice = StringId.NoticeCloseLaneBusy;
                ForceNotify();
                return;
            }
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
            check.ForegroundIsBrave = ProcessIdentity.IsBraveWindow(foreground, true);
            check.Minimized = foreground != IntPtr.Zero && NativeMethods.IsIconic(foreground);
            check.BlockingModifierDown = KeyboardInjector.BlockingModifierDown();
            check.Scan = scan;
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
            ClearHoverRegion();
            HideOverlay();
        }

        private void HideOverlay()
        {
            if (_overlay.Visible) _overlay.Hide();
        }

        private void CheckStall()
        {
            long running = Math.Max(_overlayWorker.RunningForMs(), _closeWorker.RunningForMs());
            _stalled = running > AppInfo.ScanStallMs;
            if (_stalled)
            {
                HideOverlay();
                _hover.Reset();
                if (!_stallLogged)
                {
                    _stallLogged = true;
                    AppLog.Warn("UI Automation has not returned for more than " + AppInfo.ScanStallMs + " ms; the overlay stays hidden until Brave responds.");
                }
            }
            else if (_stallLogged && running == 0)
            {
                _stallLogged = false;
                AppLog.Info("UI Automation responded again.");
            }
        }

        private void LogOutcomeIfChanged(ScanResult result)
        {
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
                || _notifiedBrave != _braveInForeground
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
            _notifiedBrave = _braveInForeground;
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
            _overlayWorker.Dispose();
            _closeWorker.Dispose();
        }
    }
}
