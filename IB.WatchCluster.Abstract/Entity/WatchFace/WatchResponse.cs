using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Response data to the watch request
    /// </summary>
    public class WatchResponse : BaseApiResponse
    {
        /// <summary>
        /// The ID of the request to track request processing
        /// </summary>
        public string RequestId { get; set; } = default!;

        /// <summary>
        /// Location name in given coordinates
        /// </summary>
        [JsonPropertyName("location")]
        public LocationInfo LocationInfo { get; set; } = new LocationInfo();

        /// <summary>
        /// Weather data in given coordinates
        /// </summary>
        [JsonPropertyName("weather")]
        public WeatherInfo WeatherInfo { get; set; } = new WeatherInfo();

        /// <summary>
        /// Exchange rate between two given currencies 
        /// </summary>
        [JsonPropertyName("exchange")]
        public ExchangeRateInfo ExchangeRateInfo { get; set; } = new ExchangeRateInfo();

        /// <summary>
        /// interval for next refresh
        /// </summary>
        [JsonPropertyName("ref-interval")] 
        public int RefInterval { get; set; } = 40;
    }
}
