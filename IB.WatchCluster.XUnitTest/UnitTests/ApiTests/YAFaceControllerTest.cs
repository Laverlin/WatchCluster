using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.Configuration;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Controllers;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace IB.WatchCluster.XUnitTest.UnitTests.ApiTests
{
    public class YAFaceControllerTest
    {
        [Fact]
        public async Task GetShoudRetunResultAfterProcessing()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<YaFaceController>>();
            var otMetricsMock = new Mock<OtelMetrics>("", "", "");
            var kafkaConfigMock = new Mock<KafkaConfiguration>();
            var activitySourceMock = new ActivitySource("test-source");
            var deliveryResult = new DeliveryResult<string, string> 
            { 
                Message = new Message<string, string> { Value = "" }, 
                Status = PersistenceStatus.Persisted 
            };
            var kafkaProducerMock = new Mock<IKafkaProducer<string, string>>();
            kafkaProducerMock
                .Setup(m => m.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>()))
                .Returns(Task.FromResult(deliveryResult))
                .Verifiable();
            var requestId = "1";
            var collectorConsumerMock = new Mock<ICollector>();
            collectorConsumerMock
                .Setup(m => m.GetCollectedMessages(It.IsAny<string>()))
                .Returns(Task.FromResult(new WatchResponse { RequestId = requestId }))
                .Verifiable();

            
            var controller = new YaFaceController(
                loggerMock.Object, otMetricsMock.Object, activitySourceMock, kafkaConfigMock.Object, kafkaProducerMock.Object, collectorConsumerMock.Object);
            controller.ControllerContext.HttpContext = new DefaultHttpContext();
            controller.HttpContext.TraceIdentifier = requestId;

            // Act
            //
            var result = await controller.Get(new WatchRequest());

            // Assert
            //
            Assert.NotNull(result.Value);
            Assert.IsType<WatchResponse>(result.Value);

            kafkaProducerMock.Verify(_ => _.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>()), Times.Once);
            collectorConsumerMock.Verify(_ => _.GetCollectedMessages(It.IsAny<string>()), Times.Once);
        }


        [Fact]
        public async Task GetShouldReturnResultInternalErrorIfRequestIdIsLost()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<YaFaceController>>();
            var otMetricsMock = new Mock<OtelMetrics>("", "", "");
            var kafkaConfigMock = new Mock<KafkaConfiguration>();
            var activitySourceMock = new ActivitySource("test-source");
            var deliveryResult = new DeliveryResult<string, string>
            {
                Message = new Message<string, string> { Value = "" },
                Status = PersistenceStatus.Persisted
            };
            var kafkaProducerMock = new Mock<IKafkaProducer<string, string>>();
            kafkaProducerMock
                .Setup(m => m.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>()))
                .Returns(Task.FromResult(deliveryResult))
                .Verifiable();
            var requestId = "1";
            var collectorConsumerMock = new Mock<ICollector>();
            collectorConsumerMock
                .Setup(m => m.GetCollectedMessages(It.IsAny<string>()))
                .Returns(Task.FromResult(new WatchResponse { RequestId = "" }))
                .Verifiable();


            var controller = new YaFaceController(
                loggerMock.Object, otMetricsMock.Object, activitySourceMock, kafkaConfigMock.Object, kafkaProducerMock.Object, collectorConsumerMock.Object);
            controller.ControllerContext.HttpContext = new DefaultHttpContext();
            controller.HttpContext.TraceIdentifier = requestId;

            // Act
            //
            var result = await controller.Get(new WatchRequest());

            // Assert
            //
            Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(503, ((ObjectResult)result.Result!).StatusCode);

            kafkaProducerMock.Verify(_ => _.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>()), Times.Once);
            collectorConsumerMock.Verify(_ => _.GetCollectedMessages(It.IsAny<string>()), Times.Once);
        }
    }
}
