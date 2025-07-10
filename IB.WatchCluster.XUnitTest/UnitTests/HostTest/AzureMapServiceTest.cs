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

public class AzureMapServiceTest
{
    private readonly AzureMapConfiguration _azureMapConfig = new()
    {
        UrlTemplate = "https://atlas.microsoft.com/search/address/reverse/json?api-version=1.0&query={0},{1}&subscription-key={2}",
        AuthKey = "test-key"
    };

    [Fact]
    public void OnSuccessShouldReturnValidObject()
    {
        // Arrange
        //
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;

        var fixture = new Fixture();
        var locationResponse =
            @"{
                ""features"": [{
                    ""properties"": {
                        ""address"": {
                            ""locality"": ""Olathe"",
                            ""adminDistricts"": [""Johnson Co.""],
                            ""countryRegion"": {
                                ""name"": ""United States""
                            }
                        }
                    }
                }]
            }";
        
        var handler = new Mock<HttpMessageHandler>();
        handler
            .SetupAnyRequest()
            .ReturnsResponse(locationResponse, "application/json");
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<AzureMapService>>();

        var azureMapService = new AzureMapService(
            loggerMock.Object,
            _azureMapConfig,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        //
        var result = azureMapService.RequestLocationName(lat, lon).Result;

        // Assert
        //
        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Equal("Olathe, United States", result.CityName);
    }

    [Fact]
    public async Task OnErrorShouldReturnErrorObject()
    {
        // Arrange
        //
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;

        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.BadRequest);
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<AzureMapService>>();

        var client = new AzureMapService(
            loggerMock.Object,
            _azureMapConfig,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        //
        var result = await client.RequestLocationName(lat, lon);

        // Assert
        //
        Assert.Equal(RequestStatusCode.Error, result.RequestStatus.StatusCode);
        Assert.Equal(400, result.RequestStatus.ErrorCode);
        Assert.Null(result.CityName);
    }

    [Fact]
    public async Task SecondCallWithSameLocationShouldReturnFromCache()
    {
        // Arrange
        //
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;
        var locationResponse =
            @"{
                ""features"": [{
                    ""properties"": {
                        ""address"": {
                            ""locality"": ""Olathe"",
                            ""adminDistricts"": [""Johnson Co.""],
                            ""countryRegion"": {
                                ""name"": ""United States""
                            }
                        }
                    }
                }]
            }";

        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<AzureMapService>>();
        var handler = new Mock<HttpMessageHandler>();
        handler
            .SetupAnyRequest()
            .ReturnsResponse(locationResponse, "application/json")
            .Verifiable();

        var client = new AzureMapService(
            loggerMock.Object,
            _azureMapConfig,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        //
        await client.ProcessAsync(new WatchRequest { DeviceId = "2", Lat = lat, Lon = lon });
        var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", Lat = lat, Lon = lon });

        // Assert
        //
        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Equal("Olathe, United States", result.CityName);

        handler.VerifyAnyRequest(Times.Exactly(1));
    }

    [Fact]
    public async Task SecondCallWithDifferentLocationShouldReturnFromServer()
    {
        // Arrange
        //
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;
        var lon2 = (decimal)-94.799713;
        var locationResponse =
            @"{
                ""features"": [{
                    ""properties"": {
                        ""address"": {
                            ""locality"": ""Olathe"",
                            ""adminDistricts"": [""Johnson Co.""],
                            ""countryRegion"": {
                                ""name"": ""United States""
                            }
                        }
                    }
                }]
            }";

        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<AzureMapService>>();
        var handler = new Mock<HttpMessageHandler>();
        handler
            .SetupAnyRequest()
            .ReturnsResponse(locationResponse, "application/json")
            .Verifiable();

        var client = new AzureMapService(
            loggerMock.Object,
            _azureMapConfig,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        //
        await client.ProcessAsync(new WatchRequest { DeviceId = "3", Lat = lat, Lon = lon });
        var result = await client.ProcessAsync(new WatchRequest { DeviceId = "3", Lat = lat, Lon = lon2 });

        // Assert
        //
        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Equal("Olathe, United States", result.CityName);

        handler.VerifyAnyRequest(Times.Exactly(2));
    }

    [Fact]
    public async Task OnUnauthorizedShouldReturnErrorObject()
    {
        // Arrange
        //
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;

        var handler = new Mock<HttpMessageHandler>();
        handler.SetupAnyRequest()
            .ReturnsResponse(HttpStatusCode.Unauthorized);
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<AzureMapService>>();

        var client = new AzureMapService(
            loggerMock.Object,
            _azureMapConfig,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        //
        var result = await client.RequestLocationName(lat, lon);

        // Assert
        //
        Assert.Equal(RequestStatusCode.Error, result.RequestStatus.StatusCode);
        Assert.Equal(401, result.RequestStatus.ErrorCode);
        Assert.Null(result.CityName);
    }

    [Fact]
    public async Task OnEmptyFeaturesShouldReturnEmptyLocationInfo()
    {
        // Arrange
        //
        var lat = (decimal)38.855652;
        var lon = (decimal)-94.799712;
        var locationResponse = @"{""features"": []}";

        var handler = new Mock<HttpMessageHandler>();
        handler
            .SetupAnyRequest()
            .ReturnsResponse(locationResponse, "application/json");
        var otMetricsMock = new Mock<OtelMetrics>("", "", "");
        var loggerMock = new Mock<ILogger<AzureMapService>>();

        var client = new AzureMapService(
            loggerMock.Object,
            _azureMapConfig,
            handler.CreateClient(),
            otMetricsMock.Object);

        // Act
        //
        var result = await client.RequestLocationName(lat, lon);

        // Assert
        //
        Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
        Assert.Null(result.CityName);
    }
}
