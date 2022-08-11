using System.Diagnostics.Metrics;

namespace IB.WatchCluster.DbSink.Infrastructure
{
    public class OtelMetrics
    {
        private Counter<long> SinkCounter { get; }

        public const string MetricName = "wc_dbs";
        public const string TagName = "type";

        public void IncrementSink<T>() => IncrementSink(typeof(T));
        public void IncrementSink(Type sinkType) => 
            SinkCounter.Add(1, new KeyValuePair<string, object?>(TagName, sinkType.Name.ToLower()));

        public OtelMetrics()
        {
            var meter = new Meter(MetricName);
            SinkCounter = meter.CreateCounter<long>(MetricName + "_count");
        }
    }
}
