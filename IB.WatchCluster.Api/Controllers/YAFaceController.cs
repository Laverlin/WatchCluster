using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Confluent.Kafka;
using System.Text.Json;
using IB.WatchCluster.Api.Services;
using IB.WatchCluster.Abstract.Entity.Configuration;
using System.Diagnostics;
using System.Text;

namespace IB.WatchCluster.Api.Controllers
{
    /// <summary>
    /// Controller for the watch face requests
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0", Deprecated = true), ApiVersion("2.0")]
    public class YAFaceController : Controller
    {
        private readonly ILogger<YAFaceController> _logger;
        private readonly IKafkaProducer<string, string> _kafkaProducer;
        private readonly ICollector _collectorConsumer;
        private readonly OtMetrics _otMetrics;
        private readonly ActivitySource _activitySource;
        private readonly KafkaConfiguration _kafkaConfiguration;

        public YAFaceController(
            ILogger<YAFaceController> logger,
            OtMetrics otMetrics,
            ActivitySource activitySource,
            KafkaConfiguration kafkaConfiguration,
            IKafkaProducer<string, string> kafkaProducer,
            ICollector collectorConsumer)
        {
            _logger = logger;
            _kafkaProducer = kafkaProducer;
            _collectorConsumer = collectorConsumer;
            _otMetrics = otMetrics;
            _activitySource = activitySource;
            _kafkaConfiguration = kafkaConfiguration;
        }

        /// <summary>
        /// Process request from the watchface and returns all requested data 
        /// </summary>
        /// <param name="watchRequest">watchface data</param>
        /// <returns>weather, location and exchange rate info</returns>
        [HttpGet, MapToApiVersion("2.0"), Authorize]
        [ProducesResponseType(typeof(WatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [RequestRateFactory(KeyField = "did", Seconds = 5)]
        public async Task<ActionResult<WatchResponse>> Get([FromQuery] WatchRequest watchRequest)
        {
            try
            {
                using var activity = _activitySource.StartActivity("Request processing");
                var requestId = Request.HttpContext.TraceIdentifier;
                var message = new Message<string, string>
                {
                    Headers = new Headers(){ new Header("activityId", Encoding.ASCII.GetBytes(activity?.Id ?? "")) },
                    Key = requestId,
                    Value = JsonSerializer.Serialize(watchRequest) 
                };

                var result = await _kafkaProducer.ProduceAsync(_kafkaConfiguration.WatchRequestTopic, message);
                if (result.Status == PersistenceStatus.Persisted)
                {
                    _otMetrics.ProducedCounter.Add(1);
                    _logger.LogDebug("{@Message} is delivered to {@TopicPartitionOffset}", result.Value, result.TopicPartitionOffset);
                }
                else 
                {
                    _logger.LogWarning("{@Message} was not delivered", result.Value);
                }
                

                var watchResponse = await _collectorConsumer.GetCollectedMessages(requestId);
                if (watchResponse.RequestId != requestId)
                {
                    _otMetrics.LostCounter.Add(1);
                    _logger.LogWarning("Request {@requestId} is lost, got {@messageRquestId}", requestId, watchResponse.RequestId);
                    throw new ApiException(503, "Server Error: temporarily unable to process request");
                }

                _otMetrics.CollectedCounter.Add(1);
                _logger.LogDebug(
                    new EventId(105, "WatchRequest"), "{@WatchRequest}, {@WatchResponse}, {@DeviceId}, {@CityName}",
                    watchRequest, watchResponse, watchRequest.DeviceId, watchResponse?.LocationInfo.CityName);
                return watchResponse!;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request processing error, {@WatchFaceRequest}", watchRequest);
                return ex.ReturnErrorResponse();
            }
        }
    }
}
