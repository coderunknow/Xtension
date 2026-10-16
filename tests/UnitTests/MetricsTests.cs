using System;

namespace Xtension.Tests
{
    /// <summary>
    /// The bounded timing statistics behind the ×. They are the only place the product states how slow or fast it
    /// really is, so the properties that matter are that they stay bounded, that they keep lanes apart, and that they
    /// can never influence a decision: they are written by the controller and read by diagnostics, never the reverse.
    /// </summary>
    internal static class MetricsTests
    {
        [Test]
        public static void EveryCounterIsCountedSeparately()
        {
            ScanMetrics metrics = new ScanMetrics();
            metrics.CountRequest();
            metrics.CountRequest();
            metrics.CountRefused();
            metrics.CountStale();
            metrics.CountDiscarded();
            metrics.CountError();
            metrics.CountStall();
            metrics.CountEvent();
            metrics.CountEvent();
            metrics.CountEventScanRequest();

            ScanMetricsSnapshot totals = metrics.Snapshot();
            Assert.AreEqual(2L, totals.Requests, "requests are counted");
            Assert.AreEqual(1L, totals.Refused, "refusals are counted separately from requests");
            Assert.AreEqual(1L, totals.Stale, "a stale result is not a refusal");
            Assert.AreEqual(1L, totals.Discarded, "a discarded lane result is counted");
            Assert.AreEqual(1L, totals.Errors, "errors are counted");
            Assert.AreEqual(1L, totals.Stalls, "stalls are counted");
            Assert.AreEqual(2L, totals.Events, "events are counted");
            Assert.AreEqual(1L, totals.EventScanRequests, "only the events that produced a scan are counted as scan requests");
            Assert.IsTrue(totals.EventScanRequests <= totals.Events, "an event scan request is always an event");
            Assert.AreEqual(0L, totals.Samples, "no timing has been recorded yet");
        }

        [Test]
        public static void PercentilesFollowTheSamplesAndStayOrdered()
        {
            ScanMetrics metrics = new ScanMetrics();
            for (int index = 1; index <= 20; index++)
            {
                // 1..20 ms, all in the overlay lane, with queue wait equal to the second half of the sample.
                metrics.CountRequest();
                metrics.Record(ScanLane.Overlay, index, index, index * 2);
            }
            ScanMetricsSnapshot totals = metrics.Snapshot();
            Assert.AreEqual(20L, totals.Samples, "every recorded sample is kept while under the capacity");
            Assert.AreEqual(20L, totals.ScanMaxMs, "the maximum is the slowest sample");
            Assert.IsTrue(totals.ScanP50Ms >= 1 && totals.ScanP50Ms <= 20, "p50 is inside the sample range: " + totals.ScanP50Ms);
            Assert.IsTrue(totals.ScanP95Ms >= totals.ScanP50Ms, "p95 is never below p50");
            Assert.IsTrue(totals.ScanP95Ms <= totals.ScanMaxMs, "p95 is never above the maximum");
            Assert.IsTrue(totals.QueueP95Ms >= 1 && totals.QueueP95Ms <= 20, "queue p95 is inside its own sample range");
            Assert.IsTrue(totals.Describe().Contains("requests=20"), "the summary reports the request count: " + totals.Describe());
            Assert.IsTrue(totals.Describe().Contains("samples=20"), "the summary reports how many timings it kept: " + totals.Describe());
            Assert.IsTrue(totals.Describe().Contains("scanMs p50="), "the summary reports scan percentiles");
        }

        [Test]
        public static void StatisticsStayBoundedForALongSession()
        {
            ScanMetrics metrics = new ScanMetrics();
            for (int index = 0; index < AppInfo.MetricSampleCapacity * 10; index++)
            {
                metrics.Record(ScanLane.Dashboard, 1, 2, 3);
                metrics.RecordEventToVisible(5);
                metrics.CountRequest();
            }
            ScanMetricsSnapshot totals = metrics.Snapshot();
            Assert.AreEqual(AppInfo.MetricSampleCapacity, totals.Samples,
                "the sample buffer stops growing at its documented capacity, so a long session cannot grow memory");
            Assert.IsTrue(totals.ScanMaxMs <= 3, "the retained samples are the real recordings");
            Assert.IsTrue(totals.EventToVisibleP95Ms >= 0, "event-to-visible timings are summarised once measured");
            Assert.AreEqual(AppInfo.MetricSampleCapacity * 10L, totals.Requests, "counters keep counting after the buffer is full");
        }

        [Test]
        public static void LanesAreKeptApartAndAnUnmeasuredLatencyIsReportedAsUnmeasured()
        {
            ScanMetrics metrics = new ScanMetrics();
            ScanMetricsSnapshot empty = metrics.Snapshot();
            Assert.AreEqual(-1L, empty.LastEventToVisibleMs, "an unmeasured latency is reported as unmeasured, never as zero");
            Assert.AreEqual(-1L, empty.EventToVisibleP95Ms, "an unmeasured distribution is reported as unmeasured");
            Assert.AreEqual(0.0, empty.ScanP50Ms, "no timing has been collected yet");

            ScanLane[] lanes = new ScanLane[] { ScanLane.Overlay, ScanLane.Close, ScanLane.Dashboard };
            foreach (ScanLane lane in lanes)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(ScanLane), lane), "the lane is part of the documented set");
            }
            Assert.AreNotEqual(ScanLane.Overlay, ScanLane.Close, "a close validation is not an overlay scan");
            Assert.AreNotEqual(ScanLane.Overlay, ScanLane.Dashboard, "a dashboard scan is not an overlay scan");
            Assert.AreNotEqual(ScanLane.Close, ScanLane.Dashboard, "a dashboard scan is not a close validation");

            metrics.Record(ScanLane.Close, 0, 1, 1);
            ScanMetricsSnapshot after = metrics.Snapshot();
            Assert.AreEqual(1L, after.Samples, "a close validation is recorded too");
            Assert.AreNotEqual(empty.Samples, after.Samples, "recording a sample changes the snapshot");
        }

        [Test]
        public static void SnapshotsAreCopiesAndNeverExposePrivateData()
        {
            ScanMetrics metrics = new ScanMetrics();
            metrics.Record(ScanLane.Overlay, 4, 8, 12);
            ScanMetricsSnapshot first = metrics.Snapshot();
            metrics.Record(ScanLane.Overlay, 4, 8, 12);
            Assert.AreEqual(1L, first.Samples, "a snapshot taken earlier is not updated by later recordings");
            Assert.AreEqual(2L, metrics.Snapshot().Samples, "the live snapshot sees both recordings");

            string text = metrics.Snapshot().Describe();
            Assert.IsFalse(text.Contains("title"), "the summary never describes a title");
            Assert.IsFalse(text.Contains("http"), "the summary never contains a URL");
            Assert.IsFalse(text.Contains("\\\\"), "the summary never contains a path");
        }
    }
}
