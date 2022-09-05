using System.Diagnostics.Metrics;
using IB.WatchCluster.Abstract;

namespace IB.WatchCluster.YasTelegramBot.Infrastructure;

public class OtelMetrics
{
    private long _serviceUptime = 0;
    private readonly Counter<long> _processedCounter;

    public OtelMetrics(string metricProject, string metricSolution = "wc", string metricJob = "WatchCluster" )
    {
        var meter = new Meter(metricJob);
        
        MetricJob = metricJob;
        
        meter.CreateObservableGauge(
            $"{metricSolution}_{metricProject}_uptime_gauge", 
            () => new Measurement<long>(_serviceUptime, new KeyValuePair<string, object?>("app", SolutionInfo.Name)));

        _processedCounter = meter.CreateCounter<long>($"{metricSolution}_{metricProject}_processed_counter");
    }

    /// <summary>
    /// Returns the job name for metric scarper
    /// </summary>
    public string MetricJob { get; }

    public void IncreaseProcessedCounter()
    {
        _processedCounter.Add(1);
    }

    public void SetInstanceUp() => _serviceUptime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public void SetInstanceDown() => _serviceUptime = 0;
    
}