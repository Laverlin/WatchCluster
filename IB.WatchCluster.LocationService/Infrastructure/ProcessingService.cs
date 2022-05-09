using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace IB.WatchCluster.LocationService.Infrastructure
{
    public class ProcessingService : BackgroundService
    {
        private readonly KafkaConfiguration _kafkaConfig;
        private readonly IConsumer<string, string> _consumer;
        private readonly KafkaProducer<string, string> _producer;
        private readonly ILogger _logger;
        private readonly ActivitySource _activitySource;

        public ProcessingService(
            KafkaConfiguration kafkaConfig, 
            IConsumer<string, string> consumer, 
            KafkaProducer<string, string> producer, 
            ILogger<ProcessingService> logger,
            ActivitySource activitySource)
        {
            _kafkaConfig = kafkaConfig;
            _consumer = consumer;
            _producer = producer;
            _logger = logger;
            _activitySource = activitySource;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _consumer.Subscribe(_kafkaConfig.WatchRequestTopic);
            try
            {
                _logger.LogInformation("Start consuming");
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var cr = _consumer.Consume(stoppingToken);
                        _logger.LogDebug(
                            "Consumed message {@Key} at: {@TopicPartitionOffset}",
                            cr.Message.Key, cr.TopicPartitionOffset.ToString());


                        using var activity = _activitySource.StartActivity("handle requested task");
                        if (cr.Message.Headers.TryGetLastBytes("activityId", out var rawActivityId))
                            activity?.SetParentId(Encoding.ASCII.GetString(rawActivityId));
                        
                        _producer.Produce(
                            _kafkaConfig.WatchResponseTopic,
                            new Message<string, string> { Key = cr.Message.Key, Value = "" },
                            dh => _logger.LogDebug(
                                "Resend message {@Key} at: {@TopicPartitionOffset}",
                                dh.Message.Key, dh.TopicPartitionOffset.ToString()));
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
