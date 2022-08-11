using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace IB.WatchCluster.Abstract.Metrics;

public class OtelMetrics
{
    private Counter<long> CounterInstance { get; }
    
    public OtelMetrics(
        string counter, string metricProject = "api", string metricSolution = "wc", string metricJob = "WatchCluster")
    {
        MetricJob = metricJob;
        var counterName = $"{metricSolution}_{metricProject}";
        
        var meter = new Meter(MetricJob);
        CounterInstance = meter.CreateCounter<long>(counterName + "_request_count");
    }
    
    public string MetricJob { get; }
    
    public void IncrementCounter(KeyValuePair<string, object>[] tags) => 
        CounterInstance.Add(1, tags);
}