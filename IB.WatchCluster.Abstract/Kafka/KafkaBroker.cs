using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Kafka.Entity;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;


namespace IB.WatchCluster.Abstract.Kafka;

public sealed class KafkaBroker: IKafkaBroker
{
    private readonly ILogger<KafkaBroker> _logger;
    private readonly KafkaConfiguration _kafkaConfiguration;
    private readonly KafkaProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public KafkaBroker(ILogger<KafkaBroker> logger, KafkaConfiguration kafkaConfiguration)
    {
        _logger = logger;
        _kafkaConfiguration = kafkaConfiguration;
        _producer = new KafkaProducer<string, string>(kafkaConfiguration);
        _consumer = new ConsumerBuilder<string, string>(kafkaConfiguration.BuildConsumerConfig()).Build();
    }

    public async Task<DeliveryResult<string, string>> ProduceYasMessageAsync<T>(string command, T msgObject)
        => await ProduceYasAsync(
            string.IsNullOrEmpty(_kafkaConfiguration.YasTopic) ? Topics.YasTopic : _kafkaConfiguration.YasTopic, 
            command, 
            msgObject);

    public async Task<DeliveryResult<string, string>> ProduceRequestAsync<T>(string key, string activityId, T msgObject)
        => await ProduceAsync(Topics.RequestTopic, key, activityId, msgObject);
    
    public async Task ProduceResponseAsync<T>(string key, string activityId, T msgObject)
        => await ProduceAsync(Topics.ResponseTopic, key, activityId, msgObject);

    public void Flush(TimeSpan timeout) => _producer.Flush(timeout);

    public void SubscribeRequests() => _consumer.Subscribe(Topics.RequestTopic);
    
    public void SubscribeResponses() => _consumer.Subscribe(Topics.ResponseTopic);

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

    public async Task StartConsumingLoop(
        string topic, 
        Func<KnownMessage, Task> messageHandler, 
        Action<ConsumerLoopStatus> consumerLoopStatus, 
        CancellationToken cancellationToken)
        => await StartConsumingLoop(new[] { topic }, messageHandler, consumerLoopStatus, cancellationToken);
    
    public async Task StartConsumingLoop(
        IEnumerable<string> topics,
        Func<KnownMessage, Task> messageHandler, 
        Action<ConsumerLoopStatus> consumerLoopStatus, 
        CancellationToken cancellationToken)
    {
        try
        {
            consumerLoopStatus(ConsumerLoopStatus.Running);
            _logger.LogInformation("Start consuming {@consumerGroup}", _kafkaConfiguration.GroupId);
            _consumer.Subscribe(topics);
            await Task.Run(() => HandleLoopMessages(messageHandler, cancellationToken), CancellationToken.None);
        }
        finally
        {
            ConsumerClose();
            consumerLoopStatus(ConsumerLoopStatus.Stopped);
            _logger.LogInformation("Stop consuming {@consumerGroup}", _kafkaConfiguration.GroupId);
        }
    }

    private void HandleLoopMessages(Func<KnownMessage, Task> messageHandler, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = Consume(cancellationToken);
                if (message == null)
                    continue;
                messageHandler(message);
            }
            catch (ConsumeException e) when (e.Error.IsFatal)
            {
                _logger.LogCritical(e, "Consume Fatal Exception {@msg}", e.Message);
                break;                
            }
            catch (ConsumeException e)
            {
                _logger.LogWarning(e, "Consume Exception {@msg}", e.Message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled exception, shutting down");
                break;
            }
        }
    }

    private async Task<DeliveryResult<string, string>> ProduceYasAsync<T>(string topic, string command, T msgObject)
    {
        var message = new Message<string, string>
        {
            Headers = new Headers
            {
                new Header("command", Encoding.ASCII.GetBytes(command))
            },
            Key = Guid.NewGuid().ToString(),
            Value = JsonSerializer.Serialize(msgObject)
        };
        
        if (Activity.Current != null)
            Propagator.Inject(
                new PropagationContext(Activity.Current.Context, Baggage.Current),
                message, (m, key, value) => m.Headers.Add(key, Encoding.ASCII.GetBytes(value))
            );
        
        var dr = await _producer.ProduceAsync(topic, message);
        if (dr.Status != PersistenceStatus.Persisted)
        {
            _logger.LogWarning("Unable to deliver the message {@Key} to a broker", message.Key);
        }
        else
        {
            _logger.LogDebug(
                "Push processed message {@Key} at: {@TopicPartitionOffset}",
                dr.Message.Key, dr.TopicPartitionOffset.ToString());
        }

        return dr;
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
        _consumer.Dispose();
        _producer.Dispose();
    }
}