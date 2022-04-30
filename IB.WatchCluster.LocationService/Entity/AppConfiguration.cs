﻿using System.ComponentModel.DataAnnotations;


namespace IB.WatchCluster.LocationService.Entity
{
    public class AppConfiguration
    {
        /// <summary>
        /// Location service url template
        /// </summary>
        [Required, Url]
        public string OpenTelemetryCollectorUrl { get; set; } = default!;
    }
}