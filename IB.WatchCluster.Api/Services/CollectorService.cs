using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Kafka;

namespace IB.WatchCluster.Api.Services;

public sealed class CollectorService: BackgroundService
{
    private readonly IConsumer<string, string> _kafkaConsumer;
    private readonly KafkaConfiguration _kafkaConfiguration;
    private readonly ILogger _logger;
    private readonly CollectorHandler _collectorHandler;

    public CollectorService(
        IConsumer<string, string> kafkaConsumer,
        KafkaConfiguration kafkaConfiguration, 
        ILogger<CollectorService> logger,
        CollectorHandler collectorHandler)
    {
        _kafkaConsumer = kafkaConsumer;
        _kafkaConfiguration = kafkaConfiguration;
        _logger = logger;
        _collectorHandler = collectorHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Start collector loop");
            _collectorHandler.IsRunning = true;
            await Task.Run(() => StartConsumerLoop(cancellationToken), CancellationToken.None);
        }
        finally
        {
            _collectorHandler.IsRunning = false;
        }
    }
    
    public void StartConsumerLoop(CancellationToken cancellationToken)
    {
        _kafkaConsumer.Subscribe(_kafkaConfiguration.WatchResponseTopic);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var cr = _kafkaConsumer.Consume(cancellationToken);
                if (!cr.Message.TryParseMessage(out var message))
                {
                    _logger.LogWarning("Unable to parse message {@message}", cr.Message);
                    continue;
                }
                _logger.LogDebug("Collector got {@message}", message);
                _collectorHandler.OnNext(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException e)
            {
                _logger.LogError(e, "Consume error: {@error}", e.Error.Reason);
                
                // https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md#fatal-consumer-errors
                //
                if (e.Error.IsFatal)
                    break;
            }
            catch (Exception e)
            {
                _logger.LogCritical(e, "Unexpected error: {@message}", e.Message);
                break;
            }
        }
        _collectorHandler.OnCompleted();
    }

    public override void Dispose()
    {
        _kafkaConsumer.Close(); // Commit offsets and leave the group cleanly.
        _kafkaConsumer.Dispose();
        base.Dispose();
    }
}