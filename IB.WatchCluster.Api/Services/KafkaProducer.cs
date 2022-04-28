using Confluent.Kafka;

namespace IB.WatchCluster.Api.Services
{
    public interface IKafkaProducer<K, V>
    {
        /// <summary>
        ///     Asychronously produce a message and expose delivery information
        ///     via the returned Task. Use this method of producing if you would
        ///     like to await the result before flow of execution continues.
        /// <summary>
        Task<DeliveryResult<K, V>> ProduceAsync(string topic, Message<K, V> message);

        /// <summary>
        ///     Asynchronously produce a message and expose delivery information
        ///     via the provided callback function. Use this method of producing
        ///     if you would like flow of execution to continue immediately, and
        ///     handle delivery information out-of-band.
        /// </summary>
        void Produce(string topic, Message<K, V> message, Action<DeliveryReport<K, V>>? deliveryHandler = null);

        void Flush(TimeSpan timeout);
    }


    public class KafkaProducer<K, V> : IKafkaProducer<K, V>
    {
        private readonly IProducer<K, V> _producer;

        public KafkaProducer(KafkaProducerCore handle)
        {
            _producer = new DependentProducerBuilder<K, V>(handle.Handle).Build();
        }

        /// <summary>
        ///     Asychronously produce a message and expose delivery information
        ///     via the returned Task. Use this method of producing if you would
        ///     like to await the result before flow of execution continues.
        /// <summary>
        public Task<DeliveryResult<K, V>> ProduceAsync(string topic, Message<K, V> message)
            => _producer.ProduceAsync(topic, message);

        /// <summary>
        ///     Asynchronously produce a message and expose delivery information
        ///     via the provided callback function. Use this method of producing
        ///     if you would like flow of execution to continue immediately, and
        ///     handle delivery information out-of-band.
        /// </summary>
        public void Produce(string topic, Message<K, V> message, Action<DeliveryReport<K, V>>? deliveryHandler = null)
            => _producer.Produce(topic, message, deliveryHandler);

        public void Flush(TimeSpan timeout)
            => _producer.Flush(timeout);
    }
}
