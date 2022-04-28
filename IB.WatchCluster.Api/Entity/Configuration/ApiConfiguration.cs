using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.Api.Entity.Configuration
{
    /// <summary>
    /// Watch API configuration setting
    /// </summary>
    internal class ApiConfiguration
    {
        /// <summary>
        /// Location service url template
        /// </summary>
        [Required, Url]
        public string OpenTelemetryCollectorUrl { get; set; } = default!;

        /// <summary>
        /// Authentication settings of the application
        /// </summary>
        [Required]
        public AuthSettings AuthSettings { get; set; } = default!;

    }
}
