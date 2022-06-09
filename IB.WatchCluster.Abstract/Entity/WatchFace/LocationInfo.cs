using LinqToDB.Mapping;
using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Name of the location based on given coordinates
    /// </summary>
    [Table("CityInfo")]
    public class LocationInfo: IHandlerResult
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
        /// Unique requestId
        /// </summary>
        [PrimaryKey]
        [Column("requestid")]
        public string RequestId { get; set; }

        /// <summary>
        /// City name in given location
        /// </summary>
        [JsonPropertyName("cityName")]
        [Column("CityName")]
        public string CityName { get; set; }

        /// <summary>
        /// Status of request to the remote server
        /// </summary>
        [JsonPropertyName("status")]
        public RequestStatus RequestStatus { get; set; } = new();
    }
}
