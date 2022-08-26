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
            var collectorHandler = new CollectorHandler();

            new Thread(() =>
            {
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-2",
                    Value = new LocationInfo("TestCity")
                });
            }).Start();


            // Act
            //
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
            var collectorHandler = new CollectorHandler();

            new Thread(() =>
            {
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-5",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-12",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext( new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-14",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader{MessageType = typeof(LocationInfo)},
                    Key = "requestId-10",
                    Value = new LocationInfo("TestCity")
                });
            }).Start();

            // Act
            //
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
            var collectorHandler = new CollectorHandler();
            new Thread(() =>
            {
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-2",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-3",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-4",
                    Value = new LocationInfo("TestCity")
                });
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                });
                Thread.Sleep(3000);
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(WeatherInfo) },
                    Key = "requestId-1",
                    Value = new WeatherInfo { WeatherProvider = "TestProvider" }
                });
                Thread.Sleep(3000);
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(ExchangeRateInfo) },
                    Key = "requestId-1",
                    Value = new ExchangeRateInfo { ExchangeRate = 2 }
                });
            }).Start();

            // Act
            //
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
            var collectorHandler = new CollectorHandler();
            new Thread(() =>
            {
                collectorHandler.OnNext(new KnownMessage
                {
                    Header = new MessageHeader { MessageType = typeof(LocationInfo) },
                    Key = "requestId-1",
                    Value = new LocationInfo("TestCity")
                });
                
                Thread.Sleep(20000);
                collectorHandler.OnNext(
                    new KnownMessage
                    {
                        Header = new MessageHeader { MessageType = typeof(WeatherInfo) },
                        Key = "requestId-1",
                        Value = new WeatherInfo { WeatherProvider = "TestProvider" }
                    });
            }).Start();

            // Act
            //
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
