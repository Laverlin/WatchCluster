using System.ComponentModel.DataAnnotations;
using IB.WatchCluster.Abstract.Services;

namespace IB.WatchCluster.ServiceHost.Entity;

public class AppConfiguration
{
    [Required]
    public string Handler { get; set; } = default!;

    /// <summary>
    /// Location service url template
    /// </summary>
    [Required]
    public string OpenTelemetryCollectorUrl { get; set; } = default!;
}