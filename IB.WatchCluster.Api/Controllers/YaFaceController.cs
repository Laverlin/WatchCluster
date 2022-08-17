using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Confluent.Kafka;
using IB.WatchCluster.Api.Services;
using IB.WatchCluster.Abstract.Entity.Configuration;
using System.Diagnostics;
using IB.WatchCluster.Abstract.Kafka;
using IB.WatchCluster.Abstract.Kafka.Entity;

namespace IB.WatchCluster.Api.Controllers;

/// <summary>
/// Controller for the watch face requests
/// </summary>
[ApiController]
[Produces("application/json")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0", Deprecated = true), ApiVersion("2.0")]
public class YaFaceController : Controller
{
    private readonly ILogger<YaFaceController> _logger;
    private readonly IKafkaProducer<string, string> _kafkaProducer;
    private readonly CollectorHandler _collectorHandler;
    private readonly OtelMetrics _otelMetrics;
    private readonly ActivitySource _activitySource;
    private readonly KafkaConfiguration _kafkaConfiguration;
    private readonly Stopwatch _processTimer = new ();
    private bool _isProduced = false;
    private bool _isCollected = false;

    public YaFaceController(
        ILogger<YaFaceController> logger,
        OtelMetrics otelMetrics,
        ActivitySource activitySource,
        KafkaConfiguration kafkaConfiguration,
        IKafkaProducer<string, string> kafkaProducer,
        CollectorHandler collectorHandler)
    {
        _logger = logger;
        _kafkaProducer = kafkaProducer;
        _collectorHandler = collectorHandler;
        _otelMetrics = otelMetrics;
        _activitySource = activitySource;
        _kafkaConfiguration = kafkaConfiguration;
    }

    /// <summary>
    /// Process request from the watchface and returns all requested data 
    /// </summary>
    /// <param name="watchRequest">watchface data</param>
    /// <returns>weather, location and exchange rate info</returns>
    [HttpGet(Name = "WatchRequest"), MapToApiVersion("2.0"), Authorize]
    [ProducesResponseType(typeof(WatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]    
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [RequestRateFactory(KeyField = "did", Seconds = 5)]
    public async Task<ActionResult<WatchResponse>> Get([FromQuery] WatchRequest watchRequest)
    {
        try
        {
            // Set the stage
            //
            using var activity = _activitySource.StartActivity($"RequestProcessing");
            var requestId = Request.HttpContext.TraceIdentifier;
            watchRequest.RequestId = requestId;
            
            // Produce a message to process
            //
            await ProduceMessage(MessageExtensions.CreateMessage(requestId, activity?.Id ?? "", watchRequest));

            // Wait for the result of processing
            //
            var watchResponse = await CollectResult(requestId);
            
            _logger.LogDebug(
                new EventId(105, "WatchRequest"), "{@WatchRequest}, {@WatchResponse}, {@DeviceId}, {@CityName}",
                watchRequest, watchResponse, watchRequest.DeviceId, watchResponse.LocationInfo.CityName);
            return watchResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request processing error, {@WatchFaceRequest}", watchRequest);
            return ex.ReturnErrorResponse();
        }
        finally
        {
            CleanupCounters();
        }
    }

    /// <summary>
    /// Produce a message to process request
    /// </summary>
    /// <param name="message"></param>
    /// <exception cref="ApiException"></exception>
    private async Task ProduceMessage(KnownMessage message)
    {
        _otelMetrics.IncrementActiveMessages();
        _processTimer.Start();
        
        var result = await _kafkaProducer.ProduceAsync(_kafkaConfiguration.WatchRequestTopic, message.ToKafkaMessage());
        if (result.Status == PersistenceStatus.Persisted)
        {
            _isProduced = true;
            _logger
                .LogDebug("{@Message} is delivered to {@TopicPartitionOffset}", result.Value, result.TopicPartitionOffset);
        }
        else
        {
            throw new ApiException(503, "Server Error: Unable deliver message to the queue");
        }
    }

    private async Task<WatchResponse> CollectResult(string requestId)
    {
        var watchResponse = await _collectorHandler.GetCollectedMessages(requestId);
        if (watchResponse.RequestId != requestId)
        {
            _logger
                .LogError("Request {@requestId} is lost, got {@messageRequestId}", requestId, watchResponse.RequestId);
            throw new ApiException(503, "Server Error: temporarily unable to process request");
        }
        _isCollected = true;
        
        return watchResponse;
    }

    private void CleanupCounters()
    {
        if (_processTimer.IsRunning)
        {
            _processTimer.Stop();
            _otelMetrics.SetMessageDuration(_processTimer.ElapsedMilliseconds);
        }
        _otelMetrics.DecrementActiveMessages();
        if (_isProduced)
            _otelMetrics.IncrementMessageCounter(new[]
            {
                new KeyValuePair<string, object?>("is-received", _isCollected),
            });
    }
}