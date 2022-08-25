using Confluent.Kafka;
using IB.WatchCluster.Abstract.Kafka;

namespace IB.WatchCluster.Api.Services;

public sealed class CollectorService: BackgroundService
{
    private readonly ILogger _logger;
    private readonly IKafkaBroker _kafkaBroker;
    private readonly CollectorHandler _collectorHandler;

    public CollectorService(
        ILogger<CollectorService> logger,
        IKafkaBroker kafkaBroker,
        CollectorHandler collectorHandler)
    {
        _logger = logger;
        _kafkaBroker = kafkaBroker;
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
        _kafkaBroker.SubscribeResponses();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = _kafkaBroker.Consume(cancellationToken);
                if (message == null)
                    continue;
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
        _kafkaBroker.ConsumerClose();
        _collectorHandler.OnCompleted();
    }

    public override void Dispose()
    {
        _kafkaBroker.Dispose();
        base.Dispose();
    }
}