using System.Diagnostics.Metrics;

namespace IB.WatchCluster.Api.Infrastructure
{
    public class OtMetrics
    {
        public Counter<long> NoTokenCounter { get; }
        public Counter<long> WrongTokenCounter { get; }
        public Counter<long> OkTokenCounter { get; }

        public Counter<long> ProducedCounter { get; }
        public Counter<long> CollectedCounter { get; }
        public Counter<long> LostCounter { get; }

        public Counter<long> MessageBufferedCounter { get; }
        public Counter<long> BufferedFoundCounter { get; }
        public Counter<long> BufferedNotFoundCounter { get; }


        public Counter<long> ActiveRequestCounter { get; }
        public Counter<long> ActiveWSRequestCounter { get; }

        private Counter<long> HttpRequestCount { get; }



        public OtMetrics(string metricJob = "WatchCluster", string metricSolution = "wc", string metricProject = "api")
        {
            var meter = new Meter(metricJob);
            MetricJob = metricJob;

            NoTokenCounter = meter.CreateCounter<long>($"{MetricJob}_token_no_token");
            WrongTokenCounter = meter.CreateCounter<long>($"{MetricJob}_token_wrong_token");
            OkTokenCounter = meter.CreateCounter<long>($"{MetricJob}_token_ok_token");

            ProducedCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_produced_count");
            CollectedCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_collected_count");
            LostCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_lost_count");

            MessageBufferedCounter = meter.CreateCounter<long>($"{MetricJob}_mesage_buffered_count");
            BufferedFoundCounter = meter.CreateCounter<long>($"{MetricJob}_buffered_found_count");
            BufferedNotFoundCounter = meter.CreateCounter<long>($"{MetricJob}_buffered_lost_count");

            ActiveRequestCounter = meter.CreateCounter<long>($"{MetricJob}_activerequest_count");
            ActiveWSRequestCounter = meter.CreateCounter<long>($"{MetricJob}_ws_activerequest_count");

            HttpRequestCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_httprequest_count");
        }

        public string MetricJob { get; }

        public void IncrementRequestCounter(KeyValuePair<string, object?>[] tags) => HttpRequestCount.Add(1, tags);
    }
}
