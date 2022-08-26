using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Kafka.Entity;

namespace IB.WatchCluster.ServiceHost.Infrastructure;

public sealed class ProcessingService<THandler>: BackgroundService where THandler: IHandlerResult
{
    private readonly KafkaBroker _kafkaBroker;
    private readonly ActivitySource _activitySource;
    private readonly IRequestHandler<THandler> _requestHandler;
    private readonly ProcessingHandler _processingHandler;

    public ProcessingService(
        OtelMetrics otelMetrics,
        ActivitySource activitySource,
        IRequestHandler<THandler> requestHandler,
        ProcessingHandler processingHandler,
        KafkaBroker kafkaBroker,
        IHostApplicationLifetime appLifetime)
    {
        _kafkaBroker = kafkaBroker;
        _activitySource = activitySource;
        _requestHandler = requestHandler;
        _processingHandler = processingHandler;
        appLifetime.ApplicationStarted.Register(otelMetrics.SetInstanceUp);
        appLifetime.ApplicationStopped.Register(otelMetrics.SetInstanceDown);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _kafkaBroker.StartConsumingLoop(
            Topics.RequestTopic, 
            MessageHandler, 
            status => _processingHandler.IsRunning = status, 
            cancellationToken);
    }

    private async Task MessageHandler(KnownMessage message)
    {
        using (_activitySource.StartActivity(typeof(THandler).Name, ActivityKind.Consumer, message.Header.ActivityId))
        {
            var result = await _requestHandler.ProcessAsync(message.Value as WatchRequest);
            result.RequestId = message.Key;
            await _kafkaBroker.ProduceResponseAsync(message.Key, message.Header.ActivityId, result);
        }
    }
    
    public override void Dispose()
    {
        _kafkaBroker.Dispose();
        base.Dispose();
    }
}