using System.Diagnostics.Metrics;

namespace IB.WatchCluster.Api.Infrastructure;

public class OtelMetrics
{
    private readonly string _metricPrefix = "wc";
    private Counter<long> RequestCounter { get; }

    public OtelMetrics(string metricProject = "api")
    {
        var counterName = $"{_metricPrefix}_{metricProject}";
        var meter = new Meter(counterName);
        RequestCounter = meter.CreateCounter<long>(counterName + "_request_count");
    }
    public void IncrementRequestCounter(KeyValuePair<string, object?>[] tags) => 
        RequestCounter.Add(1, tags);
}