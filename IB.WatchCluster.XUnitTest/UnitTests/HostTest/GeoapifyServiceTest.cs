using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoFixture;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;
using Xunit;

namespace IB.WatchCluster.XUnitTest.UnitTests.HostTest;

public class GeoapifyServiceTest
{
    private readonly GeoapifyConfiguration _config = new()
    {
        UrlTemplate = "https://api.geoapify.com/v1/geocode/reverse?lon={0}&lat={1}&apiKey={2}",
        AuthKey = "test-key"
    };

    [Fact]
    public void OnSuccessShouldReturnValidObject()
    {
        // Arrange
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;

        var locationResponse =
            @"{
                ""features"": [{
                    ""properties"": {
                        ""city"": ""Olathe"",
                        ""country"": ""United States""
                    }
                }]
            }";

        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(locationResponse, "application/json");
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<GeoapifyService>>();

        var service = new GeoapifyService(
            loggerMock.Object,
            _config,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        var result = service.RequestLocationName(lat, lon).Result;

        // Assert
        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Equal("Olathe, United States", result.CityName);
    }

    [Fact]
    public async Task OnErrorShouldReturnErrorObject()
    {
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;

        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(HttpStatusCode.BadRequest);
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<GeoapifyService>>();

        var service = new GeoapifyService(
            loggerMock.Object,
            _config,
            handler.CreateClient(),
            otMetricsMock.Object);

        var result = await service.RequestLocationName(lat, lon);

        Assert.Equal(RequestStatusCode.Error, result.RequestStatus.StatusCode);
        Assert.Equal(400, result.RequestStatus.ErrorCode);
        Assert.Null(result.CityName);
    }

    [Fact]
    public async Task SecondCallWithSameLocationShouldReturnFromCache()
    {
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;
        var locationResponse =
            @"{
                ""features"": [{
                    ""properties"": {
                        ""city"": ""Olathe"",
                        ""country"": ""United States""
                    }
                }]
            }";

        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<GeoapifyService>>();
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(locationResponse, "application/json").Verifiable();

        var service = new GeoapifyService(
            loggerMock.Object,
            _config,
            handler.CreateClient(),
            otMetricsMock.Object);

        await service.ProcessAsync(new WatchRequest { DeviceId = "g2", Lat = lat, Lon = lon });
        var result = await service.ProcessAsync(new WatchRequest { DeviceId = "g2", Lat = lat, Lon = lon });

        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Equal("Olathe, United States", result.CityName);
        handler.VerifyAnyRequest(Times.Exactly(1));
    }

    [Fact]
    public async Task SecondCallWithDifferentLocationShouldReturnFromServer()
    {
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;
        var lon2 = (decimal)-94.799713;
        var locationResponse =
            @"{
                ""features"": [{
                    ""properties"": {
                        ""city"": ""Olathe"",
                        ""country"": ""United States""
                    }
                }]
            }";

        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<GeoapifyService>>();
        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(locationResponse, "application/json").Verifiable();

        var service = new GeoapifyService(
            loggerMock.Object,
            _config,
            handler.CreateClient(),
            otMetricsMock.Object);

        await service.ProcessAsync(new WatchRequest { DeviceId = "g3", Lat = lat, Lon = lon });
        var result = await service.ProcessAsync(new WatchRequest { DeviceId = "g3", Lat = lat, Lon = lon2 });

        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Equal("Olathe, United States", result.CityName);

        handler.VerifyAnyRequest(Times.Exactly(2));
    }

    [Fact]
    public async Task OnUnauthorizedShouldReturnErrorObject()
    {
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;

        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(HttpStatusCode.Unauthorized);
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<GeoapifyService>>();

        var service = new GeoapifyService(
            loggerMock.Object,
            _config,
            handler.CreateClient(),
            otMetricsMock.Object);

        var result = await service.RequestLocationName(lat, lon);

        Assert.Equal(RequestStatusCode.Error, result.RequestStatus.StatusCode);
        Assert.Equal(401, result.RequestStatus.ErrorCode);
        Assert.Null(result.CityName);
    }

    [Fact]
    public async Task OnEmptyFeaturesShouldReturnEmptyLocationInfo()
    {
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;
    var locationResponse = @"{""features"": []}";

        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest().ReturnsResponse(locationResponse, "application/json");
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<GeoapifyService>>();

        var service = new GeoapifyService(
            loggerMock.Object,
            _config,
            handler.CreateClient(),
            otMetricsMock.Object);

        var result = await service.RequestLocationName(lat, lon);

        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Null(result.CityName);
    }
}
