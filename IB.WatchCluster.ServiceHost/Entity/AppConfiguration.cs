using System.ComponentModel.DataAnnotations;


namespace IB.WatchCluster.ServiceHost.Entity
{
    public class AppConfiguration
    {
        [Required]
        public string Handler { get; set; } = default!;

        /// <summary>
        /// Location service url template
        /// </summary>
        [Required, Url]
        public string OpenTelemetryCollectorUrl { get; set; } = default!;
    }
}
