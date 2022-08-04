using System.Diagnostics.Metrics;

namespace IB.WatchCluster.DbSink.Infrastructure
{
    public class OtelMetrics
    {
        private Counter<long> _sinkCounter { get; }

        public const string MetricName = "wc_dbs";
        public const string TagName = "type";

        public void IncrementSink<T>() => IncrementSink(typeof(T));
        public void IncrementSink(Type sinkType) => _sinkCounter.Add(1, new KeyValuePair<string, object?>(TagName, sinkType.Name.ToLower()));

        public OtelMetrics()
        {
            var meter = new Meter(MetricName);
            _sinkCounter = meter.CreateCounter<long>(MetricName + "_count");
        }
    }
}
