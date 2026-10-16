using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Xtension
{
    /// <summary>Which lane produced a timing. Kept in the core so the statistics do not depend on the UI layer.</summary>
    internal enum ScanLane
    {
        Overlay = 0,
        Close = 1,
        Dashboard = 2
    }

    /// <summary>One completed scan, as timings only. Never contains a title, a URL or provider text.</summary>
    internal struct ScanSample
    {
        public ScanLane Lane;
        /// <summary>Time from submitting the request to the worker starting it (queue wait).</summary>
        public long QueuedMs;
        /// <summary>Time the worker spent inside UI Automation.</summary>
        public long ScanMs;
        /// <summary>Time from submitting the request to the result reaching the UI thread.</summary>
        public long TotalMs;
    }

    /// <summary>Immutable copy of the counters, so a reader cannot see a half-updated set.</summary>
    internal sealed class ScanMetricsSnapshot
    {
        public long Requests;
        public long Refused;
        public long Completed;
        public long Stale;
        public long Discarded;
        public long Errors;
        public long Stalls;
        public long Events;
        public long EventScanRequests;
        public double ScanP50Ms;
        public double ScanP95Ms;
        public long ScanMaxMs;
        public double QueueP95Ms;
        public long Samples;
        /// <summary>Time from an eligibility-changing event to the × being visible, or -1 when never measured.</summary>
        public long LastEventToVisibleMs = -1;
        public long EventToVisibleP95Ms = -1;

        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append("requests=").Append(Requests).Append(", refused=").Append(Refused)
                .Append(", completed=").Append(Completed).Append(", stale=").Append(Stale)
                .Append(", discarded=").Append(Discarded).Append(", errors=").Append(Errors)
                .Append(", stalls=").Append(Stalls);
            if (Samples > 0)
            {
                text.Append(", scanMs p50=").Append(ScanP50Ms.ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" p95=").Append(ScanP95Ms.ToString("F1", CultureInfo.InvariantCulture))
                    .Append(" max=").Append(ScanMaxMs)
                    .Append(", queueMs p95=").Append(QueueP95Ms.ToString("F1", CultureInfo.InvariantCulture));
            }
            text.Append(", events=").Append(Events).Append(", samples=").Append(Samples);
            if (EventToVisibleP95Ms >= 0) text.Append(", eventToVisibleMs p95=").Append(EventToVisibleP95Ms);
            if (LastEventToVisibleMs >= 0) text.Append(", lastEventToVisibleMs=").Append(LastEventToVisibleMs);
            return text.ToString();
        }
    }

    /// <summary>
    /// Bounded response-time statistics for the scan lanes. It stores at most
    /// <see cref="AppInfo.MetricSampleCapacity"/> timings, so memory and diagnostic cost stay constant for the whole
    /// session, and it holds timings and counters only — no titles, URLs or provider text ever enter it.
    /// </summary>
    internal sealed class ScanMetrics
    {
        private readonly object _gate = new object();
        private readonly ScanSample[] _samples = new ScanSample[AppInfo.MetricSampleCapacity];
        private readonly long[] _eventToVisible = new long[AppInfo.MetricSampleCapacity / 4];
        private int _sampleCount;
        private int _sampleNext;
        private int _visibleCount;
        private int _visibleNext;
        private long _requests;
        private long _refused;
        private long _completed;
        private long _stale;
        private long _discarded;
        private long _errors;
        private long _stalls;
        private long _events;
        private long _eventScanRequests;
        private long _lastEventToVisible = -1;

        /// <summary>Completed non-close scans so far. Cheap enough for the tick path: no allocation, no copy.</summary>
        public long CompletedCount
        {
            get
            {
                lock (_gate) { return _completed; }
            }
        }

        public void CountRequest()
        {
            lock (_gate) { _requests++; }
        }

        public void CountRefused()
        {
            lock (_gate) { _refused++; }
        }

        public void CountStale()
        {
            lock (_gate) { _stale++; }
        }

        public void CountDiscarded()
        {
            lock (_gate) { _discarded++; }
        }

        public void CountError()
        {
            lock (_gate) { _errors++; }
        }

        public void CountStall()
        {
            lock (_gate) { _stalls++; }
        }

        /// <summary>An event notification from a browser or from Windows arrived.</summary>
        public void CountEvent()
        {
            lock (_gate) { _events++; }
        }

        /// <summary>An event notification actually produced a scan request.</summary>
        public void CountEventScanRequest()
        {
            lock (_gate) { _eventScanRequests++; }
        }

        public void Record(ScanLane lane, long queuedMs, long scanMs, long totalMs)
        {
            lock (_gate)
            {
                ScanSample sample = new ScanSample();
                sample.Lane = lane;
                sample.QueuedMs = queuedMs < 0 ? 0 : queuedMs;
                sample.ScanMs = scanMs < 0 ? 0 : scanMs;
                sample.TotalMs = totalMs < 0 ? 0 : totalMs;
                _samples[_sampleNext] = sample;
                _sampleNext = (_sampleNext + 1) % _samples.Length;
                if (_sampleCount < _samples.Length) _sampleCount++;
                _completed++;
            }
        }

        /// <summary>Records the time from an eligibility-changing event to the × actually being visible.</summary>
        public void RecordEventToVisible(long milliseconds)
        {
            if (milliseconds < 0) return;
            lock (_gate)
            {
                _eventToVisible[_visibleNext] = milliseconds;
                _visibleNext = (_visibleNext + 1) % _eventToVisible.Length;
                if (_visibleCount < _eventToVisible.Length) _visibleCount++;
                _lastEventToVisible = milliseconds;
            }
        }

        public ScanMetricsSnapshot Snapshot()
        {
            List<long> durations = new List<long>();
            List<long> queue = new List<long>();
            List<long> visible = new List<long>();
            ScanMetricsSnapshot metrics = new ScanMetricsSnapshot();
            lock (_gate)
            {
                for (int index = 0; index < _sampleCount; index++) durations.Add(_samples[index].ScanMs);
                for (int index = 0; index < _sampleCount; index++) queue.Add(_samples[index].QueuedMs);
                for (int index = 0; index < _visibleCount; index++) visible.Add(_eventToVisible[index]);
                metrics.Requests = _requests;
                metrics.Refused = _refused;
                metrics.Completed = _completed;
                metrics.Stale = _stale;
                metrics.Discarded = _discarded;
                metrics.Errors = _errors;
                metrics.Stalls = _stalls;
                metrics.Events = _events;
                metrics.EventScanRequests = _eventScanRequests;
                metrics.Samples = _sampleCount;
                metrics.LastEventToVisibleMs = _lastEventToVisible;
            }
            durations.Sort();
            queue.Sort();
            visible.Sort();
            metrics.ScanP50Ms = Percentile(durations, 0.50);
            metrics.ScanP95Ms = Percentile(durations, 0.95);
            metrics.ScanMaxMs = durations.Count == 0 ? 0 : durations[durations.Count - 1];
            metrics.QueueP95Ms = Percentile(queue, 0.95);
            metrics.EventToVisibleP95Ms = visible.Count == 0 ? -1 : (long)Math.Round(Percentile(visible, 0.95));
            return metrics;
        }

        private static double Percentile(List<long> sorted, double fraction)
        {
            if (sorted.Count == 0) return 0;
            int index = (int)Math.Round((sorted.Count - 1) * fraction);
            if (index < 0) index = 0;
            if (index >= sorted.Count) index = sorted.Count - 1;
            return sorted[index];
        }
    }
}
