using Confluent.Kafka;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Entity;
using IB.WatchCluster.Api.Infrastructure;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace IB.WatchCluster.Api.Services
{
    public interface ICollector
    {
        public Task<WatchResponse> GetCollectedMessages(string requestId);
    }

    public class CollectorConsumer: BackgroundService, ICollector
    {
        private readonly string _topic;
        private readonly IConsumer<string, string> _kafkaConsumer;
        private readonly ILogger _logger;
        private readonly OtMetrics _otMetrics;
        private readonly ActivitySource _activitySource;
        private readonly ReplaySubject<CollectedMessage> _messageSubject = new (TimeSpan.FromMinutes(1));

        public CollectorConsumer(
            IConsumer<string, string> kafkaConsumer, ILogger<CollectorConsumer> logger, OtMetrics otMetrics)
        {
            _topic = "incoming-request";
            _kafkaConsumer = kafkaConsumer;
            _logger = logger;
            _otMetrics = otMetrics;
            _activitySource = new ActivitySource(SolutionInfo.Name);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Start collector consumer loop");
            new Thread(() => StartConsumerLoop(stoppingToken)).Start();

            return Task.CompletedTask;
        }

        public void StartConsumerLoop(CancellationToken cancellationToken)
        {
            _kafkaConsumer.Subscribe(_topic);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cr = _kafkaConsumer.Consume(cancellationToken);
                    var message = new CollectedMessage
                    {
                        RequestId = cr.Message.Key,
                        Message = cr.Message.Value
                    };
                    _logger.LogDebug("Collector got {@message}", message);
                    _messageSubject.OnNext(message);
                    _otMetrics.MessageBufferedCounter.Add(1);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException e)
                {
                    _logger.LogError("Consume error: {@error}", e.Error.Reason);

                    if (e.Error.IsFatal)
                    {
                        // https://github.com/edenhill/librdkafka/blob/master/INTRODUCTION.md#fatal-consumer-errors
                        break;
                    }
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e, "Unexpected error");
                    break;
                }
            }
            _messageSubject.OnCompleted();
        }

        public async Task<WatchResponse> GetCollectedMessages(string requestId)
        {
            using (_activitySource.StartActivity("CollectMessages"))
            {
                var message = await _messageSubject
                    // .Where(m => m.RequestId == requestId)
                    .SkipWhile(m => m.RequestId != requestId)
                    .Take(1)
                    .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(15)))
                    .FirstOrDefaultAsync();

                if (message == null)
                {
                    _otMetrics.BufferedNotFoundCounter.Add(1);
                    _logger.LogWarning("message for {@requestId} not found in buffer", requestId);
                }
                else
                {
                    _otMetrics.BufferedFoundCounter.Add(1);
                    _logger.LogDebug("collect message {@message}", message);
                }

                var weatherInfo = new WeatherInfo();
                var locationInfo = new LocationInfo();
                var exchangeRateInfo = new ExchangeRateInfo();

                var watchResponse = new WatchResponse
                {
                    RequestId = message?.RequestId ?? "",
                    LocationInfo = locationInfo,
                    WeatherInfo = weatherInfo,
                    ExchangeRateInfo = exchangeRateInfo
                };

                return watchResponse;
            }
        }

        public override void Dispose()
        {
            _kafkaConsumer.Close(); // Commit offsets and leave the group cleanly.
            _kafkaConsumer.Dispose();
            base.Dispose();
        }
    }
}
