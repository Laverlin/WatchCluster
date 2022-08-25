using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using System.Threading;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Kafka.Entity;
using Xunit;

namespace IB.WatchCluster.XUnitTest.UnitTests.ApiTests
{
    public class CollectorTest
    {

        [Fact]
        public async void MessageFromKafkaShouldBeCollected()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<CollectorService>>();
            var kafkaBrokerMock = new Mock<IKafkaBroker>();
            
            kafkaBrokerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                    {
                        Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                        Key = "requestId-1",
                        Value = new LocationInfo("TestCity")
                    })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-2",
                    Value = new LocationInfo("TestCity")
                });

            var collectorHandler = new CollectorHandler();
            var collector = new CollectorService(loggerMock.Object, kafkaBrokerMock.Object, collectorHandler);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collectorHandler.GetCollectedMessages("requestId");


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
            var kafkaBrokerMock = new Mock<IKafkaBroker>();

            kafkaBrokerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-5",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-12",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-14",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-10",
                    Value = new LocationInfo("TestCity")
                });

            var collectorHandler = new CollectorHandler();
            var collector = new CollectorService(loggerMock.Object, kafkaBrokerMock.Object, collectorHandler);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result5 = await collectorHandler.GetCollectedMessages("requestId-5");
            var result14 = await collectorHandler.GetCollectedMessages("requestId-14");


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
            var kafkaBrokerMock = new Mock<IKafkaBroker>();
            kafkaBrokerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-2",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                    {
                        Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                        Key = "requestId-3",
                        Value = new LocationInfo("TestCity")
                    })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-4",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() =>
                {
                    Thread.Sleep(3000);
                    return new KnownMessage
                    {
                        Header = new MessageHeader { MessageType = typeof(WeatherInfo) },
                        Key = "requestId-1",
                        Value = new WeatherInfo{ WeatherProvider = "TestProvider"}
                    };

                })
                .Returns(() =>
                 {
                     Thread.Sleep(3000);
                     return new KnownMessage
                     {
                         Header = new MessageHeader { MessageType = typeof(ExchangeRateInfo) },
                         Key = "requestId-1",
                         Value = new ExchangeRateInfo{ ExchangeRate = 2}
                     };               
                 });

            //consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collectorHandler = new CollectorHandler();
            var collector = new CollectorService(loggerMock.Object, kafkaBrokerMock.Object, collectorHandler);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collectorHandler.GetCollectedMessages("requestId-1");


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
            var kafkaBrokerMock = new Mock<IKafkaBroker>();
            
            kafkaBrokerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(() => new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                })
                .Returns(() =>
                {
                    Thread.Sleep(20000);
                    return new KnownMessage
                    {
                        Header = new MessageHeader { MessageType = typeof(WeatherInfo) },
                        Key = "requestId-1",
                        Value = new WeatherInfo {WeatherProvider = "TestProvider"}
                    };

                });
            

            var collectorHandler = new CollectorHandler();
            var collector = new CollectorService(loggerMock.Object, kafkaBrokerMock.Object, collectorHandler);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collectorHandler.GetCollectedMessages("requestId-1");


            // Assert
            //
            Assert.IsType<WatchResponse>(result);
            Assert.Equal("requestId-1", result.RequestId);
            Assert.Equal("TestCity", result.LocationInfo.CityName);
            Assert.Null(result.WeatherInfo.WeatherProvider);
        }
    }
}
