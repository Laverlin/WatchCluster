using LinqToDB.Mapping;
using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Exchange rate between requested currencies
    /// </summary>
    [Table("CityInfo")]
    public class ExchangeRateInfo: IHandlerResult
    {
        /// <summary>
        /// Unique requestId
        /// </summary>
        [PrimaryKey]
        [Column("requestid")]
        public string RequestId { get; set; }

        /// <summary>
        /// Exchange rate value
        /// </summary>
        [JsonPropertyName("exchangeRate")]
        [Column("ExchangeRate")]
        public decimal ExchangeRate { get; set; }

        /// <summary>
        /// Status of request to the remote server
        /// </summary>
        [JsonPropertyName("status")]
        public RequestStatus RequestStatus { get; set; } = new();
        
        public string RemoteSource { get; set; }
    }
}
