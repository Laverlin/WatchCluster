﻿using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.Api.Entity.Configuration
{
    /// <summary>
    /// Watch API configuration setting
    /// </summary>
    public class ApiConfiguration
    {
        /// <summary>
        /// Location service url template
        /// </summary>
        [Required]
        public string OpenTelemetryCollectorUrl { get; set; } = default!;

        /// <summary>
        /// Authentication settings of the application
        /// </summary>
        [Required]
        public AuthSettings AuthSettings { get; set; } = default!;

        /// <summary>
        /// Base URL of the YAS data api
        /// </summary>
        public string YasStorageApiUrl { get; set; } = default!;

        public int RefreshInterval { get; set; } = 40;

    }
}
