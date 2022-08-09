using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace IB.WatchCluster.Abstract.Metrics;

public class OtelMetrics
{
    private Counter<long> RequestCounter { get; }

    private readonly string _metricPrefix = "wc";
    
    public void IncrementRequestCounter(KeyValuePair<string, object>[] tags) => 
        RequestCounter.Add(1, tags);

    public OtelMetrics(string metricProject = "api")
    {
        var counterName = $"{_metricPrefix}_{metricProject}";
        var meter = new Meter(counterName);
        RequestCounter = meter.CreateCounter<long>(counterName + "_request_count");
    }
}