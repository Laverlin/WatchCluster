using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.YasTelegramBot.Configuration;

public class BotConfiguration
{
    /// <summary>
    /// Location service url template
    /// </summary>
    [Required]
    public string OpenTelemetryCollectorUrl { get; set; } = default!;

    [Required]
    public string BotApiKey { get; set; } = default!;

    [Required, Url]
    public string BaseReaderApiUrl { get; set; } = default!;
}