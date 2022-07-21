using Confluent.Kafka;
using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.Abstract.Entity.Configuration
{
    /// <summary>
    /// Kafka Configuration Settings
    /// </summary>
    public class KafkaConfiguration
    {
        /// <summary>
        /// Kafka Servers addresses e.g. localhost:9092,localhost:9093
        /// </summary>
        [Required]
        public string BootstrapServers { get; set; } = default!;

        public string GroupId { get; set; } = default!;

        public string WatchRequestTopic { get; set; } = "watch-request";

        public string WatchResponseTopic { get; set; } = "watch-response";

        public AutoOffsetReset AutoOffsetReset { get; set; } = AutoOffsetReset.Latest;
    }
}
