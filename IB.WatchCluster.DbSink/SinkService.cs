using Confluent.Kafka;
using IB.WatchCluster.Abstract;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.DbSink.Infrastructure;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace IB.WatchCluster.DbSink
{
    public class SinkService : BackgroundService
    {
        private readonly KafkaConfiguration _kafkaConfig;
        private readonly IConsumer<string, string> _consumer;
        private readonly DataConnectionFactory _dataConnectionFactory;
        private readonly ILogger _logger;
        private readonly ActivitySource _activitySource;
        private readonly OtMetrics _otMetrics;

        public SinkService(
            KafkaConfiguration kafkaConfig,
            IConsumer<string, string> consumer,
            DataConnectionFactory dataConnectionFactory,
            ILogger<SinkService> logger,
            ActivitySource activitySource,
            OtMetrics otMetrics)
        {
            _kafkaConfig = kafkaConfig;
            _consumer = consumer;
            _dataConnectionFactory = dataConnectionFactory;
            _logger = logger;
            _activitySource = activitySource;
            _otMetrics = otMetrics;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _consumer.Subscribe(new[] { _kafkaConfig.WatchRequestTopic, _kafkaConfig.WatchResponseTopic });
            try
            {
                _logger.LogInformation("Start sink to {@Provider}", _dataConnectionFactory.ProviderName);
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var cr = _consumer.Consume(cancellationToken);
                        _logger.LogDebug(
                            "Consumed message {@Key} at: {@TopicPartitionOffset}",
                            cr.Message.Key, cr.TopicPartitionOffset.ToString());

                        cr.Message.Headers.TryGetLastBytes("activityId", out var rawActivityId);
                        cr.Message.Headers.TryGetLastBytes("type", out var rawMessageType);

                        using (_activitySource.StartActivity("SinkRequest", ActivityKind.Consumer, Encoding.ASCII.GetString(rawActivityId)))
                        {
                            var messageType = SolutionInfo.Assembly.GetTypes().FirstOrDefault(t => t.Name == Encoding.ASCII.GetString(rawMessageType));
                            _logger.LogDebug("Got mssage type {@messageType}", messageType);
                            if (messageType == null)
                            {
                                _logger.LogWarning("The message type of {@message} is null", cr.Message);
                                continue;
                            }

                            var message = JsonSerializer.Deserialize(cr.Message.Value, messageType);
                            if (message == null)
                            {
                                _logger.LogWarning("Can not parse {@message} of type {@type}", cr.Message.Value, messageType);
                                continue;
                            }

                            _logger.LogDebug("Push to DB {@message}", message);

                            using (var db = _dataConnectionFactory.Create())
                            {
                                if (messageType == typeof(WatchRequest))
                                {
                                    await StoreWatchRequest((WatchRequest)message);
                                }
                                else if (messageType == typeof(LocationInfo))
                                {
                                    await StoreLocationInfo((LocationInfo)message);
                                }
                                else if (messageType == typeof(WeatherInfo))
                                {
                                    await StoreWeatherInfo((WeatherInfo)message);
                                }
                                else if (messageType == typeof(ExchangeRateInfo))
                                {
                                    await StoreExchangeRateInfo((ExchangeRateInfo)message);
                                }
                                else
                                { 
                                    _logger.LogWarning("Unknown type: @{type}", messageType);
                                    continue;
                                }
                            }
                        }
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
        }


        private async Task StoreWatchRequest(WatchRequest watchRequest)
        {
            using (var db = _dataConnectionFactory.Create())
            {
                var deviceData = (await db.QueryProcAsync<DeviceData>(
                    "add_device",
                    new DataParameter("device_id", watchRequest.DeviceId ?? "unknown"),
                    new DataParameter("device_name", watchRequest.DeviceName)))
                    .Single();

                await db.GetTable<WatchRequest>()
                    .InsertOrUpdateAsync(() => new WatchRequest
                    {
                        RequestId = watchRequest.RequestId,
                        RequestTime = watchRequest.RequestTime,
                        Lat = watchRequest.Lat,
                        Lon = watchRequest.Lon,
                        BaseCurrency = watchRequest.BaseCurrency,
                        CiqVersion = watchRequest.CiqVersion,
                        TargetCurrency = watchRequest.TargetCurrency,
                        Framework = watchRequest.Framework,
                        Version = watchRequest.Version,
                        DeviceDataId = deviceData.Id,
                    }, t => new WatchRequest
                    {
                        RequestTime = watchRequest.RequestTime,
                        Lat = watchRequest.Lat,
                        Lon = watchRequest.Lon,
                        BaseCurrency = watchRequest.BaseCurrency,
                        CiqVersion = watchRequest.CiqVersion,
                        TargetCurrency = watchRequest.TargetCurrency,
                        Framework = watchRequest.Framework,
                        Version = watchRequest.Version,
                        DeviceDataId = deviceData.Id,
                    });
            }
            _otMetrics.WatchRequestSink.Add(1);
        }

        private async Task StoreLocationInfo(LocationInfo locationInfo)
        {
            using (var db = _dataConnectionFactory.Create())
            {
                await db.GetTable<LocationInfo>()
                .InsertOrUpdateAsync(() => new LocationInfo
                {
                    RequestId = locationInfo.RequestId,
                    CityName = locationInfo.CityName,
                }, t => new LocationInfo
                {
                    CityName = locationInfo.CityName,
                });
            }
            _otMetrics.LocationSink.Add(1);
        }

        private async Task StoreWeatherInfo(WeatherInfo weatherInfo)
        {
            using (var db = _dataConnectionFactory.Create())
            {
                await db.GetTable<WeatherInfo>()
                .InsertOrUpdateAsync(() => new WeatherInfo
                {
                    RequestId = weatherInfo.RequestId,
                    Temperature = weatherInfo.Temperature,
                    WindSpeed = weatherInfo.WindSpeed,
                    PrecipProbability = weatherInfo.PrecipProbability,
                }, t => new WeatherInfo
                {
                    RequestId = weatherInfo.RequestId,
                    Temperature = weatherInfo.Temperature,
                    WindSpeed = weatherInfo.WindSpeed,
                    PrecipProbability = weatherInfo.PrecipProbability,
                });
            }
            _otMetrics.WeatherSink.Add(1);
        }

        private async Task StoreExchangeRateInfo(ExchangeRateInfo exchangeRateInfo)
        {
            using (var db = _dataConnectionFactory.Create())
            {
                await db.GetTable<ExchangeRateInfo>()
                .InsertOrUpdateAsync(() => new ExchangeRateInfo
                {
                    RequestId = exchangeRateInfo.RequestId,
                    ExchangeRate = exchangeRateInfo.ExchangeRate,
                }, t => new ExchangeRateInfo
                {
                    ExchangeRate = exchangeRateInfo.ExchangeRate,
                });
            }
            _otMetrics.ExchangeRateSink.Add(1);
        }
    }
}
