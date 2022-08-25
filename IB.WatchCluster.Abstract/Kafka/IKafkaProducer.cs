using System;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace IB.WatchCluster.Abstract.Kafka;

public interface IKafkaProducerOld<TK, TV>
{
    /// <summary>
    ///     Asynchronously produce a message and expose delivery information
    ///     via the returned Task. Use this method of producing if you would
    ///     like to await the result before flow of execution continues.
    /// </summary>
    Task<DeliveryResult<TK, TV>> ProduceAsync(string topic, Message<TK, TV> message);

    /// <summary>
    ///     Asynchronously produce a message and expose delivery information
    ///     via the provided callback function. Use this method of producing
    ///     if you would like flow of execution to continue immediately, and
    ///     handle delivery information out-of-band.
    /// </summary>
    void Produce(string topic, Message<TK, TV> message, Action<DeliveryReport<TK, TV>> deliveryHandler = null);

    void Flush(TimeSpan timeout);
}