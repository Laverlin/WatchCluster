using System;
using Microsoft.AspNetCore.Mvc;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Request parameters from a watch
    /// </summary>
    public class WatchRequest
    {
        /// <summary>
        /// Latitude
        /// </summary>
        [FromQuery(Name = "lat")]
        public decimal? Lat { get; set; }

        /// <summary>
        /// Longitude
        /// </summary>
        [FromQuery(Name = "lon")]
        public decimal? Lon { get; set; }

        /// <summary>
        /// Unique Garmin device ID
        /// </summary>
        [FromQuery(Name = "did")]
        public string DeviceId { get; set; }

        /// <summary>
        /// application version
        /// </summary>
        [FromQuery(Name = "av")]
        public string Version { get; set; }

        /// <summary>
        /// Framework version
        /// </summary>
        [FromQuery(Name = "fw")]
        public string Framework { get; set; }

        /// <summary>
        /// CIQ version
        /// </summary>
        [FromQuery(Name = "ciqv")]
        public string CiqVersion { get; set; }

        /// <summary>
        /// Name of the device
        /// </summary>
        [FromQuery(Name = "dn")]
        public string DeviceName { get; set; }

        /// <summary>
        /// api key for the Darksky service
        /// </summary>
        [FromQuery(Name = "wapikey")]
        public string DarkskyKey { get; set; }

        /// <summary>
        /// Name of the weather provider
        /// </summary>
        [FromQuery(Name = "wp")]
        public string WeatherProvider { get; set; }

        [FromQuery(Name = "bc")]
        public string BaseCurrency { get; set; }

        [FromQuery(Name = "tc")]
        public string TargetCurrency { get; set; }

        public DateTime RequestTime { get; set; } = DateTime.UtcNow;
    }
}
