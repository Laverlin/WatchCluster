using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using IB.WatchCluster.Abstract.Kafka.Entity;

namespace IB.WatchCluster.Abstract.Kafka;

public interface IKafkaBroker: IDisposable
{
    public Task<DeliveryResult<string, string>> ProduceRequestAsync<T>(string key, string activityId, T msgObject);
    
    public Task ProduceResponseAsync<T>(string key, string activityId, T msgObject);

    public void Flush(TimeSpan timeout);

    public void SubscribeRequests();
    
    public void SubscribeResponses();

    public void ConsumerClose();

    public KnownMessage Consume(CancellationToken cancellationToken);

    public Task StartConsumingLoop(
        string topic,
        Func<KnownMessage, Task> messageHandler,
        Action<bool> consumerLoopStatus,
        CancellationToken cancellationToken);
}