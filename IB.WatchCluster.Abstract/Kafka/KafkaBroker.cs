using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Kafka.Entity;
using Microsoft.Extensions.Logging;

namespace IB.WatchCluster.Abstract.Kafka;

public sealed class KafkaBroker: IKafkaBroker
{
    private readonly ILogger<KafkaBroker> _logger;
    private readonly KafkaConfiguration _kafkaConfiguration;
    private readonly KafkaProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;

    public KafkaBroker(ILogger<KafkaBroker> logger, KafkaConfiguration kafkaConfiguration)
    {
        _logger = logger;
        _kafkaConfiguration = kafkaConfiguration;
        _producer = new KafkaProducer<string, string>(kafkaConfiguration);
        _consumer = new ConsumerBuilder<string, string>(kafkaConfiguration.BuildConsumerConfig()).Build();
    }

    public async Task<DeliveryResult<string, string>> ProduceRequestAsync<T>(string key, string activityId, T msgObject)
        => await ProduceAsync(_kafkaConfiguration.WatchRequestTopic, key, activityId, msgObject);
    
    public async Task ProduceResponseAsync<T>(string key, string activityId, T msgObject)
        => await ProduceAsync(_kafkaConfiguration.WatchResponseTopic, key, activityId, msgObject);

    public void Flush(TimeSpan timeout) => _producer.Flush(timeout);

    public void SubscribeRequests() => _consumer.Subscribe(_kafkaConfiguration.WatchRequestTopic);
    
    public void SubscribeResponses() => _consumer.Subscribe(_kafkaConfiguration.WatchResponseTopic);

    public void ConsumerClose() => _consumer.Close();

    public KnownMessage Consume(CancellationToken cancellationToken)
    {
        var cr = _consumer.Consume(cancellationToken);
        if (!cr.Message.TryParseMessage(out var message))
        {
            _logger.LogWarning("Unable to parse message {@message}", cr.Message);
        }
        _logger.LogDebug("Collector got {@message}", message);
        return message;
    }
        
    private async Task<DeliveryResult<string, string>> ProduceAsync<T>(string topic, string key, string activityId, T msgObject)
    {
        var message = CreateMessage(key, activityId, msgObject);
        var dr = await _producer.ProduceAsync(topic, message);
        if (dr.Status != PersistenceStatus.Persisted)
        {
            _logger.LogWarning("Unable to deliver the message {@Key} to a broker", key);
        }
        else
        {
            _logger.LogDebug(
                "Push processed message {@Key} at: {@TopicPartitionOffset}",
                dr.Message.Key, dr.TopicPartitionOffset.ToString());
        }

        return dr;
    }

    private static Message<string, string> CreateMessage<T>(string key, string activityId, T msgObject)
    {
        return new Message<string, string>
        {
            Headers = new Headers
            {
                new Header("activityId", Encoding.ASCII.GetBytes(activityId)),
                new Header("type", Encoding.ASCII.GetBytes(typeof(T).Name))
            },
            Key = key,
            Value = JsonSerializer.Serialize(msgObject)
        };
    }

    public void Dispose()
    {
        _producer.Dispose();
        _consumer.Dispose();
    }
}