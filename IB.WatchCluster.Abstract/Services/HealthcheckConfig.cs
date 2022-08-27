using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.Abstract.Services;

/// <summary>
/// HeathCheck publisher configuration
/// </summary>
public class HealthcheckConfig
{
    /// <summary>
    /// Port number where probes are published
    /// </summary>
    [Required]
    public int HttpPort { get; set; } = 80;
    
    /// <summary>
    /// The url of liveliness probe
    /// </summary>
    [RegularExpression(
        "^[/][a-zA-Z0-9/-]*[/]$|^$", 
        ErrorMessage = "If a LiveProbeUrl is not empty, it must starts and ends with '/' character.")]
    public string LiveProbeUrl { get; set; } = "/health/live/";
    
    /// <summary>
    /// The Url of the readiness probe
    /// </summary>
    [RegularExpression(
        "^[/][a-zA-Z0-9/-]*[/]$|^$", 
        ErrorMessage = "If a ReadyProbeUrl is not empty, it must starts and ends with '/' character.")]
    public string ReadyProbeUrl { get; set; } = "/health/ready/";
    
    /// <summary>
    /// Tag value to mark live checks.
    /// Live probe will touch only checks marked by this tag
    /// </summary>
    public string LiveFilterTag { get; set; } = "live";
}