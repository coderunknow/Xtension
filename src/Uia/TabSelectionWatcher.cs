using System;
using System.Threading;
using System.Windows.Automation;

namespace Xtension
{
    /// <summary>
    /// Best-effort UI Automation notification source: it asks the browser to tell Xtension when a tab becomes
    /// selected, so the × follows a tab switch immediately instead of after the next poll.
    ///
    /// Contract, and its limits:
    /// - It can only *ask for an earlier scan*. It never decides eligibility, never authorises anything and never
    ///   touches a scan result, so a wrong, late, duplicated or missing event cannot close a tab.
    /// - It runs on one dedicated background thread. Registration happens on that thread, so a provider that hangs
    ///   while registering delays nothing else; the disposal timeout is bounded and the thread is a background thread.
    /// - Every failure is recorded and leaves polling as the only source, which is exactly the previous behaviour.
    ///
    /// The handler does the smallest possible amount of work: one counter and one callback that posts to the UI thread.
    /// </summary>
    internal sealed class TabSelectionWatcher : IDisposable
    {
        private readonly object _gate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _thread;
        private readonly Action _onTabChanged;
        private readonly AutomationEventHandler _handler;
        private IntPtr _target;
        private IntPtr _registered;
        private AutomationElement _registeredRoot;
        private long _registrations;
        private long _failures;
        private long _events;
        private string _lastFailure = string.Empty;
        private volatile bool _stopping;
        private volatile bool _registeredFlag;

        public TabSelectionWatcher(Action onTabChanged)
        {
            _onTabChanged = onTabChanged;
            _handler = OnElementSelected;
            _thread = new Thread(Run);
            _thread.Name = "Xtension tab event watch";
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        /// <summary>Window whose tab selection events are wanted, or <see cref="IntPtr.Zero"/> for none.</summary>
        public void SetTarget(IntPtr window)
        {
            lock (_gate)
            {
                if (_stopping || _target == window) return;
                _target = window;
            }
            try { _wake.Set(); } catch (ObjectDisposedException) { }
        }

        public EventWatchSnapshot Snapshot()
        {
            EventWatchSnapshot snapshot = new EventWatchSnapshot();
            snapshot.Registered = _registeredFlag;
            snapshot.Registrations = Interlocked.Read(ref _registrations);
            snapshot.Failures = Interlocked.Read(ref _failures);
            snapshot.LastFailure = _lastFailure;
            return snapshot;
        }

        private void Run()
        {
            try
            {
                while (!_stopping)
                {
                    _wake.WaitOne(1000);
                    if (_stopping) break;
                    IntPtr target;
                    lock (_gate) { target = _target; }
                    if (target == _registered) continue;
                    Unregister();
                    if (target != IntPtr.Zero) Register(target);
                }
            }
            catch (Exception ex)
            {
                AppLog.WarnThrottled("uia-events", "The tab event watcher stopped: " + ScanSafety.DescribeError("TabSelectionWatcher.Run", ex));
            }
            finally
            {
                try { Unregister(); }
                catch (Exception) { }
                _wake.Dispose();
            }
        }

        private void Register(IntPtr window)
        {
            try
            {
                AutomationElement root = AutomationElement.FromHandle(window);
                if (root == null)
                {
                    Record("AutomationElement.FromHandle returned no root");
                    _registered = window;
                    return;
                }
                Automation.AddAutomationEventHandler(SelectionItemPattern.ElementSelectedEvent, root, TreeScope.Subtree, _handler);
                _registeredRoot = root;
                _registered = window;
                _registeredFlag = true;
                Interlocked.Increment(ref _registrations);
            }
            catch (Exception ex)
            {
                // A provider that refuses the subscription just means polling is the only source.
                Record(ScanSafety.DescribeError("AddAutomationEventHandler", ex));
                _registered = window;
                _registeredFlag = false;
            }
        }

        private void Unregister()
        {
            // Reset even when no root exists. Otherwise returning to the same HWND after a foreground-away
            // unsubscription looks already registered and notifications never resume.
            _registered = IntPtr.Zero;
            if (_registeredRoot == null)
            {
                _registeredFlag = false;
                return;
            }
            try
            {
                Automation.RemoveAutomationEventHandler(SelectionItemPattern.ElementSelectedEvent, _registeredRoot, _handler);
            }
            catch (Exception ex)
            {
                Record(ScanSafety.DescribeError("RemoveAutomationEventHandler", ex));
            }
            finally
            {
                _registeredRoot = null;
                _registeredFlag = false;
            }
        }

        private void OnElementSelected(object sender, AutomationEventArgs args)
        {
            Interlocked.Increment(ref _events);
            Action handler = _onTabChanged;
            if (handler == null) return;
            try
            {
                handler();
            }
            catch (Exception)
            {
                // Never let an exception travel back into the provider's event path.
            }
        }

        private void Record(string failure)
        {
            Interlocked.Increment(ref _failures);
            _lastFailure = failure;
            AppLog.WarnThrottled("uia-events", "Tab selection notifications are unavailable; polling continues. " + failure);
        }

        public void Dispose()
        {
            if (_stopping) return;
            _stopping = true;
            try { _wake.Set(); } catch (ObjectDisposedException) { }
            // Bounded wait. A provider stuck inside a registration call must not block shutdown; the thread is a
            // background thread, so the process can still exit.
            _thread.Join(AppInfo.EventWatchRegisterTimeoutMs);
        }
    }
}
