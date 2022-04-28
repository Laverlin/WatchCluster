using System.Diagnostics.Metrics;

namespace IB.WatchCluster.Api.Infrastructure
{
    public class OtMetrics
    {
        public const string MetricName = "WatchCluster";
        public Counter<long> NoTokenCounter { get; }
        public Counter<long> WrongTokenCounter { get; }
        public Counter<long> OkTokenCounter { get; }

        public Counter<long> ProducedCounter { get; }
        public Counter<long> CollectedCounter { get; }
        public Counter<long> LostCounter { get; }

        public Counter<long> MessageBufferedCounter { get; }
        public Counter<long> BufferedFoundCounter { get; }
        public Counter<long> BufferedNotFoundCounter { get; }

        public OtMetrics()
        {
            var meter = new Meter(MetricName);
            NoTokenCounter = meter.CreateCounter<long>($"{MetricName}_token_no_token");
            WrongTokenCounter = meter.CreateCounter<long>($"{MetricName}_token_wrong_token");
            OkTokenCounter = meter.CreateCounter<long>($"{MetricName}_token_ok_token");

            ProducedCounter = meter.CreateCounter<long>($"{MetricName}_mesage_produced_count");
            CollectedCounter = meter.CreateCounter<long>($"{MetricName}_mesage_collected_count");
            LostCounter = meter.CreateCounter<long>($"{MetricName}_mesage_lost_count");

            MessageBufferedCounter = meter.CreateCounter<long>($"{MetricName}_mesage_buffered_count");
            BufferedFoundCounter = meter.CreateCounter<long>($"{MetricName}_buffered_found_count");
            BufferedNotFoundCounter = meter.CreateCounter<long>($"{MetricName}_buffered_lost_count");
        }
    }
}
