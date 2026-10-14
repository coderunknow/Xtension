using System;
using System.Threading;

namespace Xtension
{
    internal enum ScanKind
    {
        /// <summary>Periodic scan that may show or hide the overlay.</summary>
        Overlay,
        /// <summary>Scan of a non-foreground Brave window for the dashboard only. Never affects the overlay.</summary>
        Dashboard,
        /// <summary>Fresh scan requested by a × click. Its result alone may authorise Ctrl+W.</summary>
        Close
    }

    internal sealed class ScanRequest
    {
        public readonly long Id;
        public readonly long StartedMs = Clock.NowMs;
        public readonly ScanKind Kind;
        /// <summary>Controller state epoch at request time. Results from an older epoch are discarded.</summary>
        public readonly long Epoch;
        public readonly IntPtr Window;
        public readonly System.Drawing.Point ClickScreen;

        public ScanRequest(long id, ScanKind kind, long epoch, IntPtr window, System.Drawing.Point clickScreen)
        {
            Id = id;
            Kind = kind;
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
    /// Runs UI Automation scans on one dedicated multithreaded thread, so a slow or hung Brave cannot freeze the tray.
    /// Holds at most one pending request and one running scan. TrySubmit refuses work while busy, so scans never overlap.
    /// </summary>
    internal sealed class ScanWorker : IDisposable
    {
        private readonly object _gate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private readonly Thread _thread;
        private readonly Action<ScanCompletion> _completed;
        private ScanRequest _pending;
        private ScanRequest _running;
        private long _runningSinceMs = Clock.Never;
        private bool _stopping;

        public ScanWorker(string name, Action<ScanCompletion> completed)
        {
            _completed = completed;
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
                    _running = request;
                    _runningSinceMs = Clock.NowMs;
                }

                ScanResult result;
                try
                {
                    result = UiaTabScanner.Scan(request.Window);
                }
                catch (Exception ex)
                {
                    result = ScanResult.Idle(request.Window, ScanOutcome.Error, "Quét UI Automation gặp lỗi bất ngờ. Đã ẩn nút ×.");
                    result.Error = ScanSafety.DescribeError("ScanWorker.Scan", ex);
                }

                lock (_gate)
                {
                    _running = null;
                    _runningSinceMs = Clock.Never;
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
