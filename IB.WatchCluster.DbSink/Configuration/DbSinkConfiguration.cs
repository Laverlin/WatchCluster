using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.DbSink.Configuration
{
    public class DbSinkConfiguration
    {
        /// <summary>
        /// Location service url template
        /// </summary>
        [Required]
        public string OpenTelemetryCollectorUrl { get; set; } = default!;
    }
}
