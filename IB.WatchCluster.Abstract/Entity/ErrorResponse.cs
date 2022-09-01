using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity
{
    /// <summary>
    /// All error responses should be described by this class
    /// </summary>
    public class ErrorResponse : BaseApiResponse
    {
        /// <summary>
        /// HTTP status Code
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; }

        /// <summary>
        /// HTTP Status code text
        /// </summary>
        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; }
        
        /// <summary>
        /// Error Description
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

    }
}
