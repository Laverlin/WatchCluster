using Confluent.Kafka;

namespace IB.WatchCluster.Api.Services
{
    public class KafkaProducerCore : IDisposable
    {
        private readonly IProducer<byte[], byte[]> _kafkaProducer;

        public KafkaProducerCore(ProducerConfig producerConfig)
        {
            _kafkaProducer = new ProducerBuilder<byte[], byte[]>(producerConfig).Build();
        }

        public Handle Handle { get => _kafkaProducer.Handle; }

        public void Dispose()
        {
            // Block until all outstanding produce requests have completed (with or without error).
            // 
            _kafkaProducer.Flush();
            _kafkaProducer.Dispose();
        }
    }
}
