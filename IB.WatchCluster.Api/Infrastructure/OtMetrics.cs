using System.Diagnostics.Metrics;

namespace IB.WatchCluster.Api.Infrastructure
{
    public class OtMetrics
    {
        public Counter<long> ProducedCounter { get; }
        public Counter<long> CollectedCounter { get; }
        public Counter<long> LostCounter { get; }

        public Counter<long> MessageBufferedCounter { get; }
        public Counter<long> BufferedFoundCounter { get; }
        public Counter<long> BufferedNotFoundCounter { get; }



        private Counter<long> HttpRequestCount { get; }
        private Histogram<long> MessageProcessDuration { get; }

        private int _activeMessages = 0;
        private Counter<long> MessageCount { get; }



        public OtMetrics(string metricJob = "WatchCluster", string metricSolution = "wc", string metricProject = "api")
        {
            var meter = new Meter(metricJob);
            MetricJob = metricJob;

            ProducedCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_produced_count");
            CollectedCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_collected_count");
            LostCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_lost_count");

            MessageBufferedCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_buffered_count");
            BufferedFoundCounter = meter.CreateCounter<long>($"{MetricJob}_buffered_found_count");
            BufferedNotFoundCounter = meter.CreateCounter<long>($"{MetricJob}_buffered_lost_count");



            HttpRequestCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_httprequest_count");
            MessageProcessDuration = meter.CreateHistogram<long>($"{metricSolution}_{metricProject}_message_duration");
            meter.CreateObservableGauge($"{metricSolution}_{metricProject}_active_messages", () => _activeMessages);
            MessageCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_message_count");
        }

        public string MetricJob { get; }
        public void IncrementRequestCounter(KeyValuePair<string, object?>[] tags) => HttpRequestCount.Add(1, tags);
        public void SetMessageDuration(long duration) => MessageProcessDuration.Record(duration);
        public void IncrementActiveMessages() => _activeMessages++;
        public void DecrementActiveMessages() => _activeMessages--;
        public void IncrementMessageCounter(KeyValuePair<string, object?>[] tags) => MessageCount.Add(1, tags);
    }
}
