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
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Api.Entity.Configuration;
using Xunit;

namespace IB.WatchCluster.XUnitTest.UnitTests.ApiTests
{
    public class YaFaceControllerTest
    {
        [Fact]
        public async Task GetShouldReturnResultAfterProcessing()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<YaFaceController>>();
            var otMetricsMock = new Mock<OtelMetrics>("", "", "");
            var apiConfigMock = new ApiConfiguration();
            var deliveryResult = new DeliveryResult<string, string> 
            { 
                Message = new Message<string, string> { Value = "" }, 
                Status = PersistenceStatus.Persisted 
            };
            var kafkaBrokerMock = new Mock<IKafkaBroker>();
            kafkaBrokerMock
                .Setup(m => m.ProduceRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<WatchRequest>()))
                .Returns(Task.FromResult(deliveryResult))
                .Verifiable();
            var requestId = "1";
            var collectorConsumerMock = new Mock<CollectorHandler>();
            collectorConsumerMock
                .Setup(m => m.GetCollectedMessages(It.IsAny<string>()))
                .Returns(Task.FromResult(new WatchResponse { RequestId = requestId }))
                .Verifiable();

            
            var controller = new YaFaceController(
                loggerMock.Object, otMetricsMock.Object, kafkaBrokerMock.Object, collectorConsumerMock.Object, apiConfigMock);
            controller.ControllerContext.HttpContext = new DefaultHttpContext();
            controller.HttpContext.TraceIdentifier = requestId;

            // Act
            //
            var result = await controller.Get(new WatchRequest());

            // Assert
            //
            Assert.NotNull(result.Value);
            Assert.IsType<WatchResponse>(result.Value);

            kafkaBrokerMock.Verify(_ => _.ProduceRequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<WatchRequest>()), Times.Once);
            collectorConsumerMock.Verify(_ => _.GetCollectedMessages(It.IsAny<string>()), Times.Once);
        }


       // [Fact]
        public async Task GetShouldReturnResultInternalErrorIfRequestIdIsLost()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<YaFaceController>>();
            var otMetricsMock = new Mock<OtelMetrics>("", "", "");
            var apiConfigMock = new ApiConfiguration();
            var deliveryResult = new DeliveryResult<string, string>
            {
                Message = new Message<string, string> { Value = "" },
                Status = PersistenceStatus.Persisted
            };
            var kafkaBrokerMock = new Mock<IKafkaBroker>();
            kafkaBrokerMock
                .Setup(m => m.ProduceRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<WatchRequest>()))
                .Returns(Task.FromResult(deliveryResult))
                .Verifiable();
            var requestId = "1";
            var collectorConsumerMock = new Mock<CollectorHandler>();
            collectorConsumerMock
                .Setup(m => m.GetCollectedMessages(It.IsAny<string>()))
                .Returns(Task.FromResult(new WatchResponse { RequestId = "" }))
                .Verifiable();


            var controller = new YaFaceController(
                loggerMock.Object, otMetricsMock.Object, kafkaBrokerMock.Object, collectorConsumerMock.Object, apiConfigMock);
            controller.ControllerContext.HttpContext = new DefaultHttpContext();
            controller.HttpContext.TraceIdentifier = requestId;

            // Act
            //
            var result = await controller.Get(new WatchRequest());

            // Assert
            //
            Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(503, ((ObjectResult)result.Result!).StatusCode);

            kafkaBrokerMock.Verify(_ => _.ProduceRequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<WatchRequest>()), Times.Once);
            collectorConsumerMock.Verify(_ => _.GetCollectedMessages(It.IsAny<string>()), Times.Once);
        }
    }
}
