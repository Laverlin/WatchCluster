using System;
using LinqToDB.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Request parameters from a watch
    /// </summary>
    [Table("CityInfo")]
    public class WatchRequest
    {
        /// <summary>
        /// Unique requestId
        /// </summary>
        [PrimaryKey]
        [Column("requestid")]
        public string RequestId { get; set; }

        /// <summary>
        /// Latitude
        /// </summary>
        [FromQuery(Name = "lat")]
        [Column("Lat")]
        public decimal? Lat { get; set; }

        /// <summary>
        /// Longitude
        /// </summary>
        [FromQuery(Name = "lon")]
        [Column("Lon")]
        public decimal? Lon { get; set; }

        /// <summary>
        /// Unique Garmin device ID
        /// </summary>
        [FromQuery(Name = "did")]
        public string DeviceId { get; set; }

        /// <summary>
        /// application version
        /// </summary>
        [Column("FaceVersion")]
        [FromQuery(Name = "av")]
        public string Version { get; set; }

        /// <summary>
        /// Framework version
        /// </summary>
        [FromQuery(Name = "fw")]
        [Column("FrameworkVersion")]
        public string Framework { get; set; }

        /// <summary>
        /// CIQ version
        /// </summary>
        [FromQuery(Name = "ciqv")]
        [Column("CIQVersion")]
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
        [Column("BaseCurrency")]
        public string BaseCurrency { get; set; }

        [FromQuery(Name = "tc")]
        [Column("TargetCurrency")]
        public string TargetCurrency { get; set; }

        [Column("RequestTime")]
        public DateTime RequestTime { get; set; } = DateTime.UtcNow;

        [Column("DeviceInfoId")]
        public int? DeviceDataId { get; set; }
    }
}
