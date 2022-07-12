using Confluent.Kafka;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Entity;
using IB.WatchCluster.Api.Infrastructure;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Text.Json;

namespace IB.WatchCluster.Api.Services
{
    public interface ICollector
    {
        public Task<WatchResponse> GetCollectedMessages(string requestId);
    }

    public class CollectorService: BackgroundService, ICollector
    {
        private readonly IConsumer<string, string> _kafkaConsumer;
        private readonly KafkaConfiguration _kafkaConfiguration;
        private readonly ILogger _logger;
        private readonly OtMetrics _otMetrics;
        private readonly ActivitySource _activitySource;
        private readonly ReplaySubject<CollectedMessage> _messageSubject = new (TimeSpan.FromMinutes(1));

        public CollectorService(
            IConsumer<string, string> kafkaConsumer, KafkaConfiguration kafkaConfiguration, ILogger<CollectorService> logger, OtMetrics otMetrics)
        {
            _kafkaConsumer = kafkaConsumer;
            _kafkaConfiguration = kafkaConfiguration;
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
            _kafkaConsumer.Subscribe(_kafkaConfiguration.WatchResponseTopic);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cr = _kafkaConsumer.Consume(cancellationToken);
                    cr.Message.Headers.TryGetLastBytes("type", out var messageType);
                    var message = new CollectedMessage
                    {
                        RequestId = cr.Message.Key,
                        Message = cr.Message.Value,
                        MessageType = Encoding.ASCII.GetString(messageType)
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
                        //
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
                    var messages = await _messageSubject
                        .SkipWhile(m => m.RequestId != requestId)
                        .Take(3)
                        .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(15)))
                        .ToArray();

                    try
                    {
                        var watchResponse = new WatchResponse
                        {
                            RequestId = messages.Select(m => m.RequestId).FirstOrDefault(),
                            LocationInfo = JsonSerializer.Deserialize<LocationInfo>(
                                messages
                                    .Where(m => m.MessageType == nameof(LocationInfo))
                                    .Select(m => m.Message)
                                    .FirstOrDefault() ?? "{}"),
                            WeatherInfo = JsonSerializer.Deserialize<WeatherInfo>(
                                messages
                                    .Where(m => m.MessageType == nameof(WeatherInfo))
                                    .Select(m => m.Message)
                                    .FirstOrDefault() ?? "{}"),
                            ExchangeRateInfo = JsonSerializer.Deserialize<ExchangeRateInfo>(
                                messages
                                    .Where(m => m.MessageType == nameof(ExchangeRateInfo))
                                    .Select(m => m.Message)
                                    .DefaultIfEmpty("{}")
                                    .Single()),
                        };
                        return watchResponse;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Collect Message Exception {@messages}", messages);
                        throw;
                    }

               
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
