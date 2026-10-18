using System;
using System.Threading;

namespace Xtension
{
    internal sealed class ScanRequest
    {
        public readonly long Id;
        /// <summary>Submission time. Every freshness rule is measured from here, so queue wait counts as age.</summary>
        public readonly long StartedMs = Clock.NowMs;
        /// <summary>When the lane began the scan. Diagnostics only; never a freshness input on its own.</summary>
        public long WorkerStartMs = Clock.Never;
        public readonly ScanLane Lane;
        /// <summary>Controller state epoch at request time. Results from an older epoch are discarded.</summary>
        public readonly long Epoch;
        public readonly IntPtr Window;
        public readonly System.Drawing.Point ClickScreen;
        public int[] ExpectedTabRuntimeId;

        public ScanRequest(long id, ScanLane lane, long epoch, IntPtr window, System.Drawing.Point clickScreen)
        {
            Id = id;
            Lane = lane;
            Epoch = epoch;
            Window = window;
            ClickScreen = clickScreen;
        }
    }

    internal sealed class ScanCompletion
    {
        public readonly ScanRequest Request;
        public readonly ScanResult Result;

        public ScanCompletion(ScanRequest request, ScanResult result)
        {
            Request = request;
            Result = result;
        }
    }

    /// <summary>
    /// Runs UI Automation scans on one dedicated multithreaded thread, so a slow or hung browser cannot freeze the tray.
    /// Holds at most one pending request and one running scan. TrySubmit refuses work while busy, so scans never overlap.
    /// </summary>
    internal sealed class ScanWorker : IDisposable
    {
        private readonly object _gate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _thread;
        private readonly Action<ScanCompletion> _completed;
        private readonly Func<IntPtr, ScanResult> _scan;
        private ScanRequest _pending;
        private ScanRequest _running;
        private long _runningSinceMs = Clock.Never;
        private bool _stopping;

        /// <summary>
        /// The scan delegate is supplied by the owner so the configured × size and inset are part of every scan
        /// decision, and so tests can inject a synthetic scanner.
        /// </summary>
        public ScanWorker(string name, Action<ScanCompletion> completed, Func<IntPtr, ScanResult> scan)
        {
            _completed = completed;
            _scan = scan;
            _thread = new Thread(Run);
            _thread.Name = name;
            _thread.IsBackground = true;
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
        }

        public bool IsBusy
        {
            get
            {
                lock (_gate) { return _pending != null || _running != null; }
            }
        }

        /// <summary>Milliseconds the current scan has been running, or 0 when idle.</summary>
        public long RunningForMs()
        {
            lock (_gate)
            {
                if (_running == null) return 0;
                return Clock.NowMs - _runningSinceMs;
            }
        }

        public bool TrySubmit(ScanRequest request)
        {
            lock (_gate)
            {
                if (_stopping || _pending != null || _running != null) return false;
                _pending = request;
                _wake.Set();
                return true;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_stopping) return;
                _stopping = true;
                _pending = null;
                _wake.Set();
            }
            // Do not wait for a stuck UIA call. The thread is a background thread, so process exit is not blocked.
            _thread.Join(300);
        }

        private void Run()
        {
            try { RunLoop(); }
            finally { _wake.Dispose(); }
        }

        private void RunLoop()
        {
            while (true)
            {
                _wake.WaitOne();
                ScanRequest request;
                lock (_gate)
                {
                    if (_stopping) return;
                    request = _pending;
                    _pending = null;
                    if (request == null) continue;
                    request.WorkerStartMs = Clock.NowMs;
                    _running = request;
                    _runningSinceMs = request.WorkerStartMs;
                }

                ScanResult result;
                try
                {
                    result = _scan(request.Window);
                }
                catch (Exception ex)
                {
                    result = ScanResult.Idle(request.Window, ScanOutcome.Error, ScanMessageKey.WorkerFailed);
                    result.Error = ScanSafety.DescribeError("ScanWorker.Scan", ex);
                }

                lock (_gate)
                {
                    _running = null;
                    _runningSinceMs = Clock.Never;
                    if (_stopping) return;
                }

                try
                {
                    _completed(new ScanCompletion(request, result));
                }
                catch (Exception ex)
                {
                    AppLog.Error("Scan result could not be delivered to the UI thread: " + ex.GetType().Name);
                }
            }
        }
    }
}
