using Confluent.Kafka;

namespace IB.WatchCluster.Abstract.Entity.Configuration
{
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
        public static ConsumerConfig BuildConsumerConfig(this KafkaConfiguration kafkaConfig, string groupId = null)
        {
            var clientId = SolutionInfo.Name;
            var consumerGroup = kafkaConfig.GroupId ?? groupId ?? SolutionInfo.Name.ToLower();

            return new ConsumerConfig 
            { 
                BootstrapServers = kafkaConfig.BootstrapServers,
                GroupId = consumerGroup,
                ClientId = clientId,
                AllowAutoCreateTopics = true,
                AutoOffsetReset = kafkaConfig.AutoOffsetReset
            };
        }
    }
}
