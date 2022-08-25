using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;

namespace IB.WatchCluster.Abstract.Kafka
{
    public sealed class KafkaProducer<TK, TV> : IDisposable
    {
        private readonly IProducer<TK, TV> _producer;

        public KafkaProducer(KafkaConfiguration kafkaConfig): this(kafkaConfig.BuildProducerConfig()) {}

        public KafkaProducer(ProducerConfig producerConfig)
        {
            _producer = new ProducerBuilder<TK, TV>(producerConfig).Build();
        }

        /// <summary>
        ///     Asynchronously produce a message and expose delivery information
        ///     via the returned Task. Use this method of producing if you would
        ///     like to await the result before flow of execution continues.
        /// </summary>
        public Task<DeliveryResult<TK, TV>> ProduceAsync(string topic, Message<TK, TV> message)
            => _producer.ProduceAsync(topic, message);

        /// <summary>
        ///     Asynchronously produce a message and expose delivery information
        ///     via the provided callback function. Use this method of producing
        ///     if you would like flow of execution to continue immediately, and
        ///     handle delivery information out-of-band.
        /// </summary>
        public void Produce(string topic, Message<TK, TV> message, Action<DeliveryReport<TK, TV>> deliveryHandler = null)
            => _producer.Produce(topic, message, deliveryHandler);

        public void Flush(TimeSpan timeout)
            => _producer.Flush(timeout);
        
        public void Dispose()
        {
            // Block until all outstanding produce requests have completed (with or without error).
            // 
            _producer.Flush();
            _producer.Dispose();
        }
    }
}
