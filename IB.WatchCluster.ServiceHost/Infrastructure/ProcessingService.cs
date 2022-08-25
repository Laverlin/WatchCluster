using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Kafka;

namespace IB.WatchCluster.ServiceHost.Infrastructure;

public class ProcessingService<THandler>: BackgroundService where THandler: IHandlerResult
{
    private readonly KafkaBroker _kafkaBroker;
    private readonly ILogger _logger;
    private readonly ActivitySource _activitySource;
    private readonly IRequestHandler<THandler> _requestHandler;
    private readonly ProcessingHandler _processingHandler;

    public ProcessingService( 
        ILogger<ProcessingService<THandler>> logger,
        OtelMetrics otelMetrics,
        ActivitySource activitySource,
        IRequestHandler<THandler> requestHandler,
        ProcessingHandler processingHandler,
        KafkaBroker kafkaBroker,
        IHostApplicationLifetime appLifetime
        )
    {
        _kafkaBroker = kafkaBroker;
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
        _kafkaBroker.SubscribeRequests();
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var message = _kafkaBroker.Consume(cancellationToken);
                if (message == null)
                    continue;

                using (_activitySource.StartActivity(typeof(THandler).Name, ActivityKind.Consumer, message.Header.ActivityId))
                {
                    var result = await _requestHandler.ProcessAsync(message.Value as WatchRequest);
                    result.RequestId = message.Key;
                    await _kafkaBroker.ProduceResponseAsync(message.Key, message.Header.ActivityId, result);
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
        _kafkaBroker.ConsumerClose();
    }
}