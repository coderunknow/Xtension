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
    /// Safety model:
    /// - The overlay is shown only after a Ready scan of the current foreground Brave window.
    /// - Every state change bumps an epoch. Results from an older epoch are discarded.
    /// - A click sends Ctrl+W only if a fresh scan confirms the click landed on the × of the one selected horizontal tab.
    /// - After a keystroke, the overlay is hidden and clicks are ignored for CloseCooldownMs.
    /// </summary>
    internal sealed class ActiveTabController : IDisposable
    {
        private readonly CloseButtonOverlay _overlay;
        private readonly Control _invoker;
        private readonly ScanWorker _overlayWorker;
        private readonly ScanWorker _closeWorker;

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
        private string _idleReason = "Đang khởi động. Mở Brave để nhận diện tab đang chọn.";
        private string _notice = string.Empty;
        private ScanResult _latest;
        private ScanOutcome _loggedOutcome = ScanOutcome.Idle;
        private string _loggedError;
        private long _lastOverlayRequestMs = Clock.Never;
        private long _lastDashboardRequestMs = Clock.Never;
        private long _lastConfirmedMs = Clock.Never;
        private long _lastSentMs = Clock.Never;
        private bool _closeInFlight;
        private long _closeRequestId;
        private string _overlayFailure = string.Empty;
        private long _closeStartedMs = Clock.Never;

        /// <summary>Raised on the UI thread whenever something visible may have changed.</summary>
        public event EventHandler Changed;

        /// <summary>Raised on the UI thread when a close keystroke could not be delivered.</summary>
        public event Action<string> CloseFailed;

        public ActiveTabController(CloseButtonOverlay overlay, Control invoker, bool enabled)
        {
            _overlay = overlay;
            _invoker = invoker;
            _enabled = enabled;
            _overlayWorker = new ScanWorker("Xtension UIA overlay scan", Post);
            _closeWorker = new ScanWorker("Xtension UIA close validation", Post);
            _overlay.Clicked += OnOverlayClicked;
        }

        /// <summary>Called by the UI timer every ForegroundPollMs. Cheap when Brave is not in front.</summary>
        public void Tick()
        {
            if (_disposed) return;
            long now = Clock.NowMs;

            IntPtr foreground = NativeMethods.GetForegroundRootWindow();
            if (foreground != _foreground)
            {
                _foreground = foreground;
                InvalidateConfirmation();
            }

            if (_closeInFlight && Clock.HasElapsed(_closeStartedMs, AppInfo.CloseValidationTimeoutMs))
            {
                _notice = "Xác nhận nhấp đã hết hạn. Không tab nào bị đóng.";
                InvalidateConfirmation();
            }
            bool isBrave = ProcessIdentity.IsBraveWindow(foreground);
            bool minimized = foreground != IntPtr.Zero && NativeMethods.IsIconic(foreground);
            bool available = isBrave && !minimized;
            if (_braveInForeground != available) InvalidateConfirmation();
            _braveInForeground = available;
            if (isBrave) _lastBraveWindow = foreground;

            if (!_enabled)
            {
                HideOverlay();
                _targetWindow = IntPtr.Zero;
                CheckStall();
                NotifyChanged();
                return;
            }

            if (!_braveInForeground)
            {
                HideOverlay();
                _targetWindow = IntPtr.Zero;
                _idleReason = minimized
                    ? "Cửa sổ Brave đang thu nhỏ. Khôi phục Brave để hiện nút ×."
                    : "Brave chưa ở phía trước. Chuyển sang Brave để hiện nút ×; mở bảng điều khiển để xem trạng thái gần nhất.";
                RequestDashboardScanIfVisible(now);
                CheckStall();
                NotifyChanged();
                return;
            }

            _targetWindow = foreground;
            _idleReason = string.Empty;

            // Hide when the confirmation is older than the stale window (for example, a stalled UIA call).
            if (_overlay.Visible && Clock.HasElapsed(_lastConfirmedMs, AppInfo.ScanStaleMs)) HideOverlay();

            if (!_closeInFlight && !_overlayWorker.IsBusy && Clock.HasElapsed(_lastOverlayRequestMs, AppInfo.ScanIntervalMs))
            {
                _lastOverlayRequestMs = now;
                Submit(_overlayWorker, ScanKind.Overlay, foreground, Point.Empty);
            }

            CheckStall();
            NotifyChanged();
        }

        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;
            _closeInFlight = false;
            _notice = string.Empty;
            InvalidateConfirmation();
            if (!enabled) _latest = null;
            AppLog.Info(enabled ? "Overlay enabled by user." : "Overlay disabled by user.");
            NotifyChanged();
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
            snapshot.IdleReason = _idleReason;
            snapshot.Notice = _notice;
            snapshot.Scan = _latest;
            snapshot.HiddenReason = GetHiddenReason();
            return snapshot;
        }

        private string GetHiddenReason()
        {
            if (_overlay.Visible) return string.Empty;
            if (!_enabled) return "Nút × đang tắt.";
            if (_stalled) return "UI Automation chưa phản hồi; nút × bị ẩn. Không có lệnh đóng tab.";
            if (!_braveInForeground) return _idleReason;
            if (_closeInFlight) return "Đang xác nhận lần nhấp; nút × tạm ẩn.";
            if (!Clock.HasElapsed(_lastSentMs, AppInfo.CloseCooldownMs)) return "Đang chờ sau lệnh đóng tab để tránh đóng liên tiếp.";
            if (!string.IsNullOrEmpty(_overlayFailure)) return _overlayFailure;
            if (_latest == null) return "Đang chờ quét cho trạng thái cửa sổ hiện tại.";
            if (_latest.Outcome != ScanOutcome.Ready) return _latest.Message;
            return "Kết quả xác nhận đã cũ hoặc cửa sổ đã thay đổi; đang chờ quét mới.";
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
                NotifyChanged();
                return;
            }

            if (request.Kind == ScanKind.Dashboard)
            {
                _latest = completion.Result;
                LogOutcomeIfChanged(completion.Result);
                NotifyChanged();
                return;
            }

            if (!_enabled || !_braveInForeground || _closeInFlight || request.Window != _targetWindow) return;
            if (NativeMethods.GetForegroundRootWindow() != request.Window || NativeMethods.IsIconic(request.Window)
                || !ProcessIdentity.IsBraveWindow(request.Window))
            {
                InvalidateConfirmation();
                NotifyChanged();
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
                    ShowOverlayFor(result);
                }
            }
            else
            {
                HideOverlay();
            }

            LogOutcomeIfChanged(result);
            NotifyChanged();
        }

        private void ShowOverlayFor(ScanResult result)
        {
            Rectangle target = TabGeometry.GetOverlayBounds(result.SelectedHorizontalTab.Bounds, result.Dpi);
            if (target.IsEmpty)
            {
                HideOverlay();
                return;
            }
            string failure;
            if (!_overlay.ShowAt(target, out failure))
            {
                _overlayFailure = "Không hiện được nút ×: " + failure;
                AppLog.WarnThrottled("overlay-show", failure);
            }
            else _overlayFailure = string.Empty;
        }

        private void OnOverlayClicked(Point screenPoint)
        {
            if (_disposed || !_enabled) return;

            // Hide first: the button must not stay clickable while its validation is pending.
            HideOverlay();
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
                _notice = "Brave không còn ở phía trước. Không tab nào bị đóng.";
                NotifyChanged();
                return;
            }

            InvalidateConfirmation();
            ScanRequest request = new ScanRequest(++_nextRequestId, ScanKind.Close, _epoch, foreground, screenPoint);
            if (!_closeWorker.TrySubmit(request))
            {
                AppLog.Warn("Close validation lane was busy; the click was ignored.");
                return;
            }
            _closeRequestId = request.Id;
            _closeInFlight = true;
            _closeStartedMs = Clock.NowMs;
            _notice = "Đang xác nhận tab đang chọn…";
            NotifyChanged();
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
                _notice = "Trạng thái cửa sổ đã thay đổi. Không tab nào bị đóng.";
                NotifyChanged();
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

            if (scan.WindowBounds != NativeMethods.GetWindowBounds(request.Window)
                || scan.Dpi != NativeMethods.GetWindowDpi(request.Window)) check.EpochCurrent = false;
            CloseDecision decision = CloseGuard.Evaluate(check);
            if (!decision.Allowed)
            {
                AppLog.Info("Close click ignored: " + decision.Reason + " (scan outcome " + scan.Outcome + ").");
                _notice = "Lần nhấp × bị bỏ qua vì chưa xác nhận chắc chắn tab đang chọn. Không tab nào bị đóng.";
                _latest = scan;
                NotifyChanged();
                return;
            }

            string failure;
            // Re-read immediately before injection; never rely only on the last timer tick.
            if (NativeMethods.GetForegroundRootWindow() != request.Window || KeyboardInjector.BlockingModifierDown()
                || !ScanSafety.IsFresh(request.StartedMs, Clock.NowMs, AppInfo.CloseValidationTimeoutMs))
            {
                InvalidateConfirmation();
                _notice = "Cửa sổ hoặc phím bổ trợ đã thay đổi. Không tab nào bị đóng.";
                NotifyChanged();
                return;
            }
            bool sent = KeyboardInjector.SendControlW(out failure);
            _lastSentMs = check.NowMs;
            InvalidateConfirmation();
            _latest = scan;

            if (sent)
            {
                AppLog.Info("Ctrl+W sent to the confirmed active tab; close cooldown started.");
                _notice = "Đã gửi lệnh đóng tab đang chọn.";
            }
            else
            {
                AppLog.Error("Ctrl+W was not delivered: " + failure);
                _notice = "Không gửi được lệnh đóng tab. Mở Chẩn đoán để xem chi tiết.";
                Action<string> failed = CloseFailed;
                if (failed != null) failed(_notice);
            }
            NotifyChanged();
        }

        /// <summary>Any event that makes pending results or the visible overlay untrustworthy. Hides the overlay.</summary>
        private void InvalidateConfirmation()
        {
            _epoch++;
            _closeInFlight = false;
            _closeRequestId = 0;
            _latest = null;
            _overlayFailure = string.Empty;
            _lastConfirmedMs = Clock.Never;
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
            AppLog.Info("Overlay state: " + result.Outcome + " (tabs=" + result.Tabs.Count + ", nodes=" + result.NodesVisited + ", ms=" + result.ElapsedMs + ")" + detail + ".");
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
