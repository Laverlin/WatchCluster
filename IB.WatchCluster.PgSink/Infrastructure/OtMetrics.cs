using System.Diagnostics.Metrics;

namespace IB.WatchCluster.DbSink.Infrastructure
{
    public class OtMetrics
    {
        public const string MetricName = "WatchCluster_dbs";
        public Counter<long> LocationSink { get; }
        public Counter<long> WeatherSink { get; }
        public Counter<long> ExchangeRateSink { get; }

        public Counter<long> WatchRequestSink { get; }


        public OtMetrics()
        {
            var meter = new Meter(MetricName);
            /*
            foreach(var property in GetType().GetProperties())
            {
                property.SetValue(property, meter.CreateCounter<long>(MetricName + property.Name));
            }
            */
            LocationSink = meter.CreateCounter<long>(MetricName + "_location_sink");
            WeatherSink = meter.CreateCounter<long>(MetricName + "_weather_sink");
            ExchangeRateSink = meter.CreateCounter<long>(MetricName + "_exchange_rate_sink");
            WatchRequestSink = meter.CreateCounter<long>(MetricName + "_watch_request_sink");
        }
    }
}
