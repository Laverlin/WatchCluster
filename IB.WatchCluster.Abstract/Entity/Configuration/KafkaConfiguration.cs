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
    }

    /// <summary>
    /// Build Producer/consumer configs from kafka config
    /// </summary>
    public static class KafkaConfigurationExtensions
    {
        /// <summary>
        /// Builds <see cref="ProducerConfig"/>
        /// </summary>
        /// <param name="kafkaConfig">loaded & verified kafka config</param>
        public static ProducerConfig BuildProducerConfig(this KafkaConfiguration kafkaConfig)
        {
            return new ProducerConfig { BootstrapServers = kafkaConfig.BootstrapServers };
        }

        /// <summary>
        /// Builds <see cref="ConsumerConfig"/>
        /// </summary>
        /// <param name="kafkaConfig">loaded & verified kafka config</param>
        public static ConsumerConfig BuildConsumerConfig(this KafkaConfiguration kafkaConfig)
        {
            return new ConsumerConfig { 
                BootstrapServers = kafkaConfig.BootstrapServers,
                GroupId = "tmp-consumerGroup"
            };
        }
    }
}
