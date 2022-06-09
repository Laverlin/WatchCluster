using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace IB.WatchCluster.ServiceHost.Infrastructure
{
    public class ProcessingService<TRequest>: BackgroundService where TRequest: IHandlerResult
    {
        private readonly KafkaConfiguration _kafkaConfig;
        private readonly IConsumer<string, string> _consumer;
        private readonly KafkaProducer<string, string> _producer;
        private readonly ILogger _logger;
        private readonly ActivitySource _activitySource;
        private readonly IRequestHandler<TRequest> _requestHandler;

        public ProcessingService(
            KafkaConfiguration kafkaConfig, 
            IConsumer<string, string> consumer, 
            KafkaProducer<string, string> producer, 
            ILogger<ProcessingService<TRequest>> logger,
            ActivitySource activitySource,
            IRequestHandler<TRequest> requestHandler)
        {
            _kafkaConfig = kafkaConfig;
            _consumer = consumer;
            _producer = producer;
            _logger = logger;
            _activitySource = activitySource;
            _requestHandler = requestHandler;
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _consumer.Subscribe(_kafkaConfig.WatchRequestTopic);
            try
            {
                _logger.LogInformation("Start consuming {@Type}", typeof(TRequest).Name);
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var cr = _consumer.Consume(cancellationToken);
                        _logger.LogDebug(
                            "Consumed message {@Key} at: {@TopicPartitionOffset}",
                            cr.Message.Key, cr.TopicPartitionOffset.ToString());

                        cr.Message.Headers.TryGetLastBytes("activityId", out var rawActivityId);
                        var activity = _activitySource
                            .StartActivity($"Handle requested by {typeof(TRequest).Name}", ActivityKind.Consumer, Encoding.ASCII.GetString(rawActivityId));
                        
                        var watchRequest = JsonSerializer.Deserialize<WatchRequest>(cr.Message.Value);
                        if (watchRequest == null)
                        {
                            _logger.LogWarning("Can not parse {@WatchRequest}", cr.Message.Value);
                            continue;
                        }

                        _requestHandler.ProcessAsync(watchRequest)
                            .ContinueWith(async t =>
                            {
                                var result = t.Result;
                                result.RequestId = watchRequest.RequestId;
                                var dr = await _producer.ProduceAsync(
                                    _kafkaConfig.WatchResponseTopic,
                                    new Message<string, string>
                                    {
                                        Headers = new Headers() 
                                        { 
                                            new Header("type", Encoding.ASCII.GetBytes(typeof(TRequest).Name)),
                                            new Header("activityId", rawActivityId)
                                        },
                                        Key = cr.Message.Key,
                                        Value = JsonSerializer.Serialize(result)
                                    });
                                _logger.LogDebug(
                                    "Resend message {@Key} at: {@TopicPartitionOffset}", dr.Message.Key, dr.TopicPartitionOffset.ToString());
                             }, cancellationToken)
                            .ContinueWith(_ => activity?.Stop(), cancellationToken);

                    }
                    catch (ConsumeException e)
                    {
                        _logger.LogWarning(e, "Consume Exception");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ensure the consumer leaves the group cleanly and final offsets are committed.
                //
                _consumer.Close();
            }
            return Task.CompletedTask;
        }
    }
}
