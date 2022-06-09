using LinqToDB.Mapping;
using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    public interface IHandlerResult
    {
        /// <summary>
        /// Unique requestId
        /// </summary>
        [PrimaryKey]
        public string RequestId { get; set; }

        /// <summary>
        /// Status of request to the remote server
        /// </summary>
        [JsonPropertyName("status")]
        public RequestStatus RequestStatus { get; set; }
    }
}
