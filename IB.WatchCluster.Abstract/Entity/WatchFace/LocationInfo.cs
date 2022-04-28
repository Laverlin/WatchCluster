using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Name of the location based on given coordinates
    /// </summary>
    public class LocationInfo
    {
        public LocationInfo() { }

        /// <summary>
        /// Once we have a city, the request was successful
        /// </summary>
        /// <param name="cityName">City name in given location</param>
        public LocationInfo(string cityName)
        {
            CityName = cityName;
            RequestStatus = new RequestStatus(RequestStatusCode.Ok);
        }

        /// <summary>
        /// City name in given location
        /// </summary>
        [JsonPropertyName("cityName")]
        public string CityName { get; set; }

        /// <summary>
        /// Status of request to the remote server
        /// </summary>
        [JsonPropertyName("status")]
        public RequestStatus RequestStatus { get; set; } = new();
    }
}
