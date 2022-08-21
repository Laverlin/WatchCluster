using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Kafka;

namespace IB.WatchCluster.ServiceHost.Infrastructure;

public class ProcessingService<THandler>: BackgroundService where THandler: IHandlerResult
{
    private readonly KafkaConfiguration _kafkaConfig;
    private readonly IConsumer<string, string> _consumer;
    private readonly IKafkaProducer<string, string> _producer;
    private readonly ILogger _logger;
    private readonly ActivitySource _activitySource;
    private readonly IRequestHandler<THandler> _requestHandler;
    private readonly ProcessingHandler _processingHandler;

    public ProcessingService(
        KafkaConfiguration kafkaConfig, 
        IConsumer<string, string> consumer, 
        IKafkaProducer<string, string> producer, 
        ILogger<ProcessingService<THandler>> logger,
        ActivitySource activitySource,
        IRequestHandler<THandler> requestHandler,
        ProcessingHandler processingHandler,
        IHostApplicationLifetime appLifetime,
        OtelMetrics otelMetrics)
    {
        _kafkaConfig = kafkaConfig;
        _consumer = consumer;
        _producer = producer;
        _logger = logger;
        _activitySource = activitySource;
        _requestHandler = requestHandler;
        _processingHandler = processingHandler;
        appLifetime.ApplicationStarted.Register(otelMetrics.SetInstanceUp);
        appLifetime.ApplicationStopped.Register(otelMetrics.SetInstanceDown);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _processingHandler.IsRunning = true;
            _logger.LogInformation("Start consuming {@Type}", typeof(THandler).Name);
            await Task.Run(() => HandleMessageLoop(cancellationToken), CancellationToken.None);
        }
        finally
        {
            _processingHandler.IsRunning = false;
            _logger.LogInformation("Stop consuming {@Type}", typeof(THandler).Name);
        }
    }

    private async Task HandleMessageLoop(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_kafkaConfig.WatchRequestTopic);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer.Consume(cancellationToken);
                if (!cr.Message.TryParseMessage(out var message) ||
                    message.Header.MessageType.Name != nameof(WatchRequest))
                {
                    _logger.LogWarning("Unable to parse message {@msg}", cr.Message);
                    continue;
                }

                using (_activitySource.StartActivity(typeof(THandler).Name, ActivityKind.Consumer, message.Header.ActivityId))
                {
                    var result = await _requestHandler.ProcessAsync(message.Value as WatchRequest);
                    result.RequestId = message.Key;
                    var dr = await _producer.ProduceAsync(
                        _kafkaConfig.WatchResponseTopic,
                        MessageExtensions
                            .CreateMessage(message.Key, message.Header.ActivityId, result)
                            .ToKafkaMessage());
                    _logger.LogDebug(
                        "Push processed message {@Key} at: {@TopicPartitionOffset}",
                        dr.Message.Key, dr.TopicPartitionOffset.ToString());
                }
            }
            catch (ConsumeException e)
            {
                _logger.LogWarning(e, "Consume Exception {@msg}", e.Message);
                if (e.Error.IsFatal)
                    break;
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
        _consumer.Close();
    }
}