using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading;
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
            var loggerMock = new Mock<ILogger<CollectorConsumer>>();
            var otMetricsMock = new Mock<OtMetrics>();

            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .Setup(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId", Value = "" } })
                .Verifiable();
            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorConsumer(consumerMock.Object, loggerMock.Object, otMetricsMock.Object);

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
            var loggerMock = new Mock<ILogger<CollectorConsumer>>();
            var otMetricsMock = new Mock<OtMetrics>();

            var i = 0;
            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .SetupSequence(m => m.Consume(It.IsAny<CancellationToken>()))
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-1", Value = "" } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-5", Value = "" } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-12", Value = "" } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-14", Value = "" } })
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-10", Value = "" } });

            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorConsumer(consumerMock.Object, loggerMock.Object, otMetricsMock.Object);

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
            var loggerMock = new Mock<ILogger<CollectorConsumer>>();
            var otMetricsMock = new Mock<OtMetrics>();

            var i = 0;
            var consumerMock = new Mock<IConsumer<string, string>>();
            consumerMock
                .Setup(m => m.Consume(It.IsAny<CancellationToken>()))
                .Callback(() => Thread.Sleep(3000))
                .Returns(new ConsumeResult<string, string> { Message = new Message<string, string> { Key = $"requestId-1", Value = "" } });

            consumerMock.Setup(m => m.Subscribe(It.IsAny<string>()));

            var collector = new CollectorConsumer(consumerMock.Object, loggerMock.Object, otMetricsMock.Object);

            // Act
            //
            new Thread(() => collector.StartConsumerLoop(CancellationToken.None)).Start();
            var result = await collector.GetCollectedMessages("requestId-1");


            // Assert
            //
            Assert.IsType<WatchResponse>(result);
            Assert.Equal("requestId-1", result.RequestId);
        }
    }
}
