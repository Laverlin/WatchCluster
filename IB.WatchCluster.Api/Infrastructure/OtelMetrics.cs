using System.Diagnostics.Metrics;
using IB.WatchCluster.Abstract;

namespace IB.WatchCluster.Api.Infrastructure;

public class OtelMetrics
{
    private Counter<long> HttpRequestCount { get; }
    private Histogram<long> MessageProcessDuration { get; }

    private int _activeMessages = 0;
    private Counter<long> MessageCount { get; }

    private long _serviceUptime = 0;



    public OtelMetrics(string metricJob = "WatchCluster", string metricSolution = "wc", string metricProject = "api")
    {
        var meter = new Meter(metricJob);
        MetricJob = metricJob;
            
        HttpRequestCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_httprequest_count");
        MessageProcessDuration = meter.CreateHistogram<long>($"{metricSolution}_{metricProject}_message_duration");
        meter.CreateObservableGauge($"{metricSolution}_{metricProject}_active_messages", () => _activeMessages);
        MessageCount = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_message_count");
        meter.CreateObservableGauge(
            $"{metricSolution}_{metricProject}_uptime_gauge", 
            () => new Measurement<long>(_serviceUptime, new KeyValuePair<string, object?>("app", SolutionInfo.Name)));
    }

    public string MetricJob { get; }
    public void IncrementRequestCounter(KeyValuePair<string, object?>[] tags) => HttpRequestCount.Add(1, tags);
    public void SetMessageDuration(long duration) => MessageProcessDuration.Record(duration);
    public void IncrementActiveMessages() => _activeMessages++;
    public void DecrementActiveMessages() => _activeMessages--;
    public void IncrementMessageCounter(KeyValuePair<string, object?>[] tags) => MessageCount.Add(1, tags);

    public void SetInstanceUp() => _serviceUptime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public void SetInstanceDown() => _serviceUptime = 0;
}