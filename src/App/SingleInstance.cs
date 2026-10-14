using System;
using System.Threading;

namespace Xtension
{
    /// <summary>
    /// Holds the per-session mutex that makes Xtension a single instance. A second launch asks the running
    /// instance to show its dashboard through a named auto-reset event, then exits without a dialog.
    /// </summary>
    internal sealed class SingleInstance : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly bool _owned;

        private SingleInstance(Mutex mutex, bool owned)
        {
            _mutex = mutex;
            _owned = owned;
        }

        /// <summary>True when another Xtension process already holds the mutex.</summary>
        public bool IsSecondary
        {
            get { return !_owned; }
        }

        public static SingleInstance Acquire()
        {
            try
            {
                bool createdNew;
                Mutex mutex = new Mutex(true, AppInfo.SingleInstanceMutexName, out createdNew);
                return new SingleInstance(mutex, createdNew);
            }
            catch (Exception ex)
            {
                // Without a mutex the app still runs; duplicate protection is lost, which is logged for diagnosis.
                AppLog.Error("Single-instance mutex could not be created: " + ex.GetType().Name + ": " + ex.Message);
                return new SingleInstance(null, true);
            }
        }

        /// <summary>Asks the running instance to show its dashboard. Returns false if that instance cannot be reached.</summary>
        public bool SignalExisting()
        {
            try
            {
                using (EventWaitHandle request = EventWaitHandle.OpenExisting(AppInfo.ShowDashboardEventName))
                {
                    request.Set();
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Warn("Running instance did not respond to the show request: " + ex.GetType().Name);
                return false;
            }
        }

        public void Dispose()
        {
            if (_mutex == null) return;
            try
            {
                if (_owned) _mutex.ReleaseMutex();
            }
            catch (Exception ex)
            {
                AppLog.Warn("Mutex could not be released cleanly: " + ex.GetType().Name);
            }
            finally
            {
                _mutex.Dispose();
            }
        }
    }

    /// <summary>Listens for "show dashboard" requests from later launches. Blocks in the kernel; uses no CPU while idle.</summary>
    internal sealed class ShowDashboardListener : IDisposable
    {
        private readonly EventWaitHandle _request;
        private readonly Thread _thread;
        private volatile bool _stopped;

        public ShowDashboardListener(Action onRequest)
        {
            _request = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ShowDashboardEventName);
            _thread = new Thread(delegate() { Run(onRequest); });
            _thread.Name = "Xtension show-dashboard listener";
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void Run(Action onRequest)
        {
            while (!_stopped)
            {
                _request.WaitOne();
                if (_stopped) return;
                try
                {
                    onRequest();
                }
                catch (Exception ex)
                {
                    AppLog.Error("Show-dashboard request failed: " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        public void Dispose()
        {
            _stopped = true;
            // Wake the listener so it observes _stopped. The thread is a background thread and is not joined.
            try { _request.Set(); } catch (ObjectDisposedException) { }
        }
    }
}
