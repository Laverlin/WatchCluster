using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.DbSink.Infrastructure;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Database;
using IB.WatchCluster.Abstract.Kafka.Entity;

namespace IB.WatchCluster.DbSink
{
    public class SinkService : BackgroundService
    {
        private readonly DataConnectionFactory _dataConnectionFactory;
        private readonly SinkServiceHandler _sinkServiceHandler;
        private readonly ILogger _logger;
        private readonly ActivitySource _activitySource;
        private readonly OtelMetrics _otelMetrics;
        private readonly KafkaBroker _kafkaBroker;

        public SinkService(
            ILogger<SinkService> logger,
            ActivitySource activitySource,
            OtelMetrics otelMetrics,             
            KafkaBroker kafkaBroker,
            DataConnectionFactory dataConnectionFactory,
            SinkServiceHandler sinkServiceHandler)
        {
            _dataConnectionFactory = dataConnectionFactory;
            _sinkServiceHandler = sinkServiceHandler;
            _logger = logger;
            _activitySource = activitySource;
            _otelMetrics = otelMetrics;
            _kafkaBroker = kafkaBroker;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            await _kafkaBroker.StartConsumingLoop(
                Topics.AllWfTopics,
                MessageHandler,
                status => _sinkServiceHandler.IsRunning = status == ConsumerLoopStatus.Running,
                cancellationToken);
        }

        private async Task MessageHandler(KnownMessage message)
        {
            using (_activitySource.StartActivity("SinkRequest"))
            {
                _logger.LogDebug("Push to DB {@message}", message);
                
                switch (message.Value)
                {
                    case WatchRequest msg: await SinkMessage(msg); break;
                    case LocationInfo msg: await SinkMessage(msg); break;
                    case WeatherInfo msg: await SinkMessage(msg); break;
                    case ExchangeRateInfo msg: await SinkMessage(msg); break;
                    default:
                        _logger.LogWarning("Unknown type: @{type}", message.Header.MessageType);
                        break;
                }
                _otelMetrics.IncrementSink(message.Header.MessageType);
            }
        }

        private async Task SinkMessage(WatchRequest watchRequest)
        {
            await using var db = _dataConnectionFactory.Create();
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

        private async Task SinkMessage(LocationInfo locationInfo)
        {
            await using var db = _dataConnectionFactory.Create();
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

        private async Task SinkMessage(WeatherInfo weatherInfo)
        {
            await using var db = _dataConnectionFactory.Create();
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

        private async Task SinkMessage(ExchangeRateInfo exchangeRateInfo)
        {
            await using var db = _dataConnectionFactory.Create();
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
    }
}
