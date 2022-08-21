using System.Diagnostics.Metrics;
using IB.WatchCluster.Abstract;

namespace IB.WatchCluster.Api.Infrastructure;

public class OtelMetrics
{
    private readonly Counter<long> _httpRequestCount;
    private readonly Histogram<long> _messageProcessDuration;
    private readonly Counter<long> _messageCount;
    private int _activeMessages = 0;
    private long _serviceUptime = 0;
    private int _memoryItemCount = 0;
    
    public OtelMetrics(string metricJob = "WatchCluster", string metricSolution = "wc", string metricProject = "api")
    {
        var meter = new Meter(metricJob);
        MetricJob = metricJob;
            
        _httpRequestCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_httprequest_count");
        _messageProcessDuration = meter.CreateHistogram<long>($"{metricSolution}_{metricProject}_message_duration");
        meter.CreateObservableGauge($"{metricSolution}_{metricProject}_active_messages", () => _activeMessages);
        _messageCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_message_count");
        meter.CreateObservableGauge(
            $"{metricSolution}_{metricProject}_uptime_gauge", 
            () => new Measurement<long>(_serviceUptime, new KeyValuePair<string, object?>("app", SolutionInfo.Name)));
        meter.CreateObservableGauge(
            $"{metricSolution}_{metricProject}_memoryitems_gauge", 
            () => new Measurement<long>(_memoryItemCount, new KeyValuePair<string, object?>("app", SolutionInfo.Name)));
    }

    /// <summary>
    /// Returns the job name for metric scarper
    /// </summary>
    public string MetricJob { get; }
    
    /// <summary>
    /// Increments the counter of requests to web server
    /// </summary>
    /// <param name="tags">Set of tags for the counter</param>
    public void IncrementRequestCounter(KeyValuePair<string, object?>[] tags) => _httpRequestCount.Add(1, tags);
    
    /// <summary>
    /// Set the duration of the request processing in ms
    /// </summary>
    public void SetMessageDuration(long duration) => _messageProcessDuration.Record(duration);
    
    /// <summary>
    /// increment message in process counter
    /// </summary>
    public void IncrementActiveMessages() => _activeMessages++;
    
    /// <summary>
    /// decrement message in process counter
    /// </summary>
    public void DecrementActiveMessages() => _activeMessages--;
    
    /// <summary>
    /// Increment message counter
    /// </summary>
    public void IncrementMessageCounter(KeyValuePair<string, object?>[] tags) => _messageCount.Add(1, tags);
    
    public void SetInstanceUp() => _serviceUptime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public void SetInstanceDown() => _serviceUptime = 0;
    
    /// <summary>
    /// Set count of items holding in memory cache
    /// </summary>
    public void SetMemoryCacheGauge(int count) => _memoryItemCount = count;
}