using IB.WatchCluster.Abstract.Entity.WatchFace;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Logging;
using AutoFixture;
using IB.WatchCluster.ServiceHost.Infrastructure;
using System.Threading;
using Moq.Contrib.HttpClient;
using System.Net;
using IB.WatchCluster.ServiceHost.Entity;

namespace IB.WatchCluster.XUnitTest.UnitTests.ApiTests
{
    public class VirtualEarthServiceTest
    {
        private readonly VirtualEarthConfiguration _virtualEarthConfig = new()
        {
            UrlTemplate = "https://dev.virtualearth.net/REST/v1/Locations/{0},{1}?o=json&includeEntityTypes=PopulatedPlace,AdminDivision1,AdminDivision2,CountryRegion&key={2}"
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
                 "{\"resourceSets\": [{\"resources\": [{\"name\": \"Olathe, United States\", \"address\": { \"adminDistrict\": \"KS\",\"adminDistrict2\": \"Johnson Co.\",\"countryRegion\": \"United States\",\"formattedAddress\": \"Olathe, United States\",\"locality\": \"Olathe\"}}]}]}";


            var handler = new Mock<HttpMessageHandler>();
            handler
                .SetupAnyRequest()
                .ReturnsResponse(locationResponse, "application/json");
            var otMetricsMock = new Mock<OtMetrics>();
            var loggerMock = new Mock<ILogger<VirtualEarthService>>();


            var virtualEarth = new VirtualEarthService(
                loggerMock.Object,
                handler.CreateClient(),
                _virtualEarthConfig,
                otMetricsMock.Object);

            // Act
            //
            var result = virtualEarth.RequestLocationName(lat, lon).Result;

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
            var otMetricsMock = new Mock<OtMetrics>();
            var loggerMock = new Mock<ILogger<VirtualEarthService>>();

            var client = new VirtualEarthService(
                loggerMock.Object,
                handler.CreateClient(),
                _virtualEarthConfig,
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
             "{\"resourceSets\": [{\"resources\": [{\"name\": \"Olathe, United States\", \"address\": { \"adminDistrict\": \"KS\",\"adminDistrict2\": \"Johnson Co.\",\"countryRegion\": \"United States\",\"formattedAddress\": \"Olathe, United States\",\"locality\": \"Olathe\"}}]}]}";


            var otMetricsMock = new Mock<OtMetrics>();
            var loggerMock = new Mock<ILogger<VirtualEarthService>>();
            var handler = new Mock<HttpMessageHandler>();
            handler
                .SetupAnyRequest()
                .ReturnsResponse(locationResponse, "application/json")
                .Verifiable();

            var client = new VirtualEarthService(
                loggerMock.Object,
                handler.CreateClient(),
                _virtualEarthConfig,
                otMetricsMock.Object);

            // Act
            //
            await client.ProcessAsync(new WatchRequest { DeviceId="2", Lat=lat, Lon=lon});
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
             "{\"resourceSets\": [{\"resources\": [{\"name\": \"Olathe, United States\", \"address\": { \"adminDistrict\": \"KS\",\"adminDistrict2\": \"Johnson Co.\",\"countryRegion\": \"United States\",\"formattedAddress\": \"Olathe, United States\",\"locality\": \"Olathe\"}}]}]}";


            var otMetricsMock = new Mock<OtMetrics>();
            var loggerMock = new Mock<ILogger<VirtualEarthService>>();
            var handler = new Mock<HttpMessageHandler>();
            handler
                .SetupAnyRequest()
                .ReturnsResponse(locationResponse, "application/json")
                .Verifiable();

            var client = new VirtualEarthService(
                loggerMock.Object,
                handler.CreateClient(),
                _virtualEarthConfig,
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

    }
}
