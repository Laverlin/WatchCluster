using System.Diagnostics.Metrics;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.Abstract.Entity.WatchFace;

namespace IB.WatchCluster.ServiceHost.Infrastructure;

public class OtelMetrics
{
    private long _serviceUptime = 0;
    private int _memoryItemCount = 0;
    private readonly Counter<long> _processedCounter;

    public OtelMetrics(string metricProject, string metricSolution = "wc", string metricJob = "WatchCluster" )
    {
        var meter = new Meter(metricJob);
        MetricJob = metricJob;
        
        meter.CreateObservableGauge(
            $"{metricSolution}_{metricProject}_uptime_gauge", 
            () => new Measurement<long>(_serviceUptime, new KeyValuePair<string, object?>("app", SolutionInfo.Name)));
        meter.CreateObservableGauge(
            $"{metricSolution}_{metricProject}_memoryitems_gauge", 
            () => new Measurement<long>(_memoryItemCount, new KeyValuePair<string, object?>("app", SolutionInfo.Name)));
        _processedCounter = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_processed_counter");
    }

    /// <summary>
    /// Returns the job name for metric scarper
    /// </summary>
    public string MetricJob { get; }

    public void IncreaseProcessedCounter(
        DataSourceKind sourceKind,
        RequestStatusCode statusCode,
        string remoteSource,
        IEnumerable<KeyValuePair<string, object?>> tags = default!)
    {
        var defaultTags = new[]
        {
            new KeyValuePair<string, object?>("source-kind", sourceKind.ToString()),
            new KeyValuePair<string, object?>("status", statusCode.ToString()),
            new KeyValuePair<string, object?>("remote-source", remoteSource)
        };
        tags = tags ?? Enumerable.Empty<KeyValuePair<string, object?>>();
        _processedCounter.Add(1, defaultTags.Concat(tags).ToArray());
    }

    public void SetInstanceUp() => _serviceUptime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public void SetInstanceDown() => _serviceUptime = 0;
    
    /// <summary>
    /// Set count of items holding in memory cache
    /// </summary>
    public void SetMemoryCacheGauge(int count) => _memoryItemCount = count;
}