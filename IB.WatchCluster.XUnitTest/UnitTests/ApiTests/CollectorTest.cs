using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Threading;
using Xunit;
using OtMetrics = IB.WatchCluster.Api.Infrastructure.OtMetrics;

namespace IB.WatchCluster.XUnitTest.UnitTests.ApiTests
{
    public class CollectorTest
    {
        private readonly string _messageLocation = "{\"cityName\":\"TestCity\",\"status\":{\"statusCode\":1,\"errorDescription\":null,\"errorCode\":0}}";
        private readonly string _messageWeather = "{\"weatherProvider\":\"TestProvider\",\"icon\": null, \"precipProbability\": 55, \"temperature\": 0, \"windSpeed\": 0, \"humidity\": 0, \"pressure\": 0, \"requestStatus\": {\"statusCode\": 1, \"errorDescription\": null, \"errorCode\": 0}}";
        private readonly string _messageExchange = "{\"exchangeRate\":2,\"status\":{\"statusCode\":1,\"errorDescription\":null,\"errorCode\":0}}";
        private readonly Header _headerLocation = new("type", Encoding.ASCII.GetBytes(nameof(LocationInfo)));
        private readonly Header _headerWeather = new("type", Encoding.ASCII.GetBytes(nameof(WeatherInfo)));
        private readonly Header _headerExchange = new("type", Encoding.ASCII.GetBytes(nameof(ExchangeRateInfo)));

        [Fact]
        public async void MessageFromKafkaShouldBeCollected()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<CollectorService>>();
            var otMetricsMock = new Mock<OtMetrics>("", "", "");
            var kafkaConfigMock = new Mock<KafkaConfiguration>();

            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId", Value = _messageLocation } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-1", Value = _messageLocation } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-2", Value = _messageLocation } });
            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorService(consumerMock.Object, kafkaConfigMock.Object, loggerMock.Object, otMetricsMock.Object);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collector.GetCollectedMessages("requestId");


            // Assert
            //
            Assert.IsType<WatchResponse>(result);
            Assert.Equal("requestId", result.RequestId);
        }

        [Fact]
        public async void InSeriesOfMessagesProperOneShouldBeCollected()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<CollectorService>>();
            var otMetricsMock = new Moq.Mock<OtMetrics>("", "", "");
            var kafkaConfigMock = new Mock<KafkaConfiguration>();

            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-1", Headers = new Headers { _headerLocation }, Value = _messageLocation } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-5", Headers = new Headers { _headerLocation }, Value = _messageLocation } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-12", Headers = new Headers { _headerLocation }, Value = _messageLocation } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-14", Headers = new Headers { _headerLocation }, Value = _messageLocation } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-10", Headers = new Headers { _headerLocation }, Value = _messageLocation } });

            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorService(consumerMock.Object, kafkaConfigMock.Object, loggerMock.Object, otMetricsMock.Object);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result5 = await collector.GetCollectedMessages("requestId-5");
            var result14 = await collector.GetCollectedMessages("requestId-14");


            // Assert
            //
            Assert.IsType<WatchResponse>(result5);
            Assert.Equal("requestId-5", result5.RequestId);
            Assert.IsType<WatchResponse>(result14);
            Assert.Equal("requestId-14", result14.RequestId);

        }

        [Fact]
        public async void ConsumerShouldWaitIfMessageIsNotDelivered()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<CollectorService>>();
            var otMetricsMock = new Mock<OtMetrics>("", "", "");
            var kafkaConfigMock = new Mock<KafkaConfiguration>();

            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-2", Value = _messageLocation } };
                })
                .Returns(() =>
                {
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-3", Value = _messageLocation } };
                })
                .Returns(() =>
                {
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-4", Value = _messageLocation } };
                })
                .Returns(() =>
                {
                    Thread.Sleep(3000);
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-1", Value = _messageLocation } };
                })
                .Returns(() =>
                {
                    Thread.Sleep(3000);
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerWeather }, Key = $"requestId-1", Value = _messageWeather } };
                })
                .Returns(() =>
                 {
                     Thread.Sleep(3000);
                     return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerExchange }, Key = $"requestId-1", Value = _messageExchange } };
                 });

            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorService(consumerMock.Object, kafkaConfigMock.Object, loggerMock.Object, otMetricsMock.Object);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collector.GetCollectedMessages("requestId-1");


            // Assert
            //
            Assert.IsType<WatchResponse>(result);
            Assert.Equal("requestId-1", result.RequestId);
            Assert.Equal("TestCity", result.LocationInfo.CityName);
            Assert.Equal("TestProvider", result.WeatherInfo.WeatherProvider);
            Assert.Equal(2, result.ExchangeRateInfo.ExchangeRate);
        }

        [Fact]
        public async void IfOneMessageIsNotDeliveredRestShouldProcessed()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<CollectorService>>();
            var otMetricsMock = new Mock<OtMetrics>("","","");
            var kafkaConfigMock = new Mock<KafkaConfiguration>();

            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerLocation }, Key = $"requestId-1", Value = _messageLocation } };
                })
                .Returns(() =>
                {
                    Thread.Sleep(20000);
                    return new ConsumeResult<string, string> { Message = new Message<string, string> { Headers = new Headers { _headerWeather }, Key = $"requestId-1", Value = _messageWeather } };
                });

            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorService(consumerMock.Object, kafkaConfigMock.Object, loggerMock.Object, otMetricsMock.Object);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collector.GetCollectedMessages("requestId-1");


            // Assert
            //
            Assert.IsType<WatchResponse>(result);
            Assert.Equal("requestId-1", result.RequestId);
            Assert.Equal("TestCity", result.LocationInfo.CityName);
            Assert.Null(result.WeatherInfo.WeatherProvider);
        }
    }
}
