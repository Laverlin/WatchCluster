using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Kafka.Entity;

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
        await _kafkaBroker.StartConsumingLoop(
            Topics.ResponseTopic, MessageHandler, status => _collectorHandler.IsRunning = status, cancellationToken);
        _collectorHandler.OnCompleted();
    }

    private Task MessageHandler(KnownMessage knownMessage)
    {
        _logger.LogDebug("Collector got {@message}", knownMessage);
        _collectorHandler.OnNext(knownMessage);
        return Task.CompletedTask;
    }
    
    public override void Dispose()
    {
        _kafkaBroker.Dispose();
        base.Dispose();
    }
}