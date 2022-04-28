using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Controllers;
using IB.WatchCluster.Api.Infrastructure;
using IB.WatchCluster.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
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
            var loggerMock = new Mock<ILogger<YAFaceController>>();
            var otMetricsMock = new Mock<OtMetrics>();
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

            
            var controller = new YAFaceController(
                loggerMock.Object, otMetricsMock.Object, kafkaProducerMock.Object, collectorConsumerMock.Object);
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
        public async Task GetShoudRetunResultInternalErrorIfRequestIdIsLost()
        {
            // Arrange
            //
            var loggerMock = new Mock<ILogger<YAFaceController>>();
            var otMetricsMock = new Mock<OtMetrics>();
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


            var controller = new YAFaceController(
                loggerMock.Object, otMetricsMock.Object, kafkaProducerMock.Object, collectorConsumerMock.Object);
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
