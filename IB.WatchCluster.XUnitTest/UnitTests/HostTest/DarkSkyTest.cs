using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Infrastructure;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace IB.WatchCluster.XUnitTest.UnitTests.HostTest
{
    public class DarkSkyTest
    {
        [Fact]
        public async Task OnSuccessShouldReturnValidObject()
        {
            // Arrange
            //
            var lat = (decimal)38.855652;
            var lon = (decimal)-94.799712;
            var token = "test-token";

            var otMetricsMock = new Mock<OtMetrics>();
            var loggerMock = new Mock<ILogger<WeatherService>>();

            var handler = new Mock<HttpMessageHandler>();
            var darkSkyResponse =
                "{\"currently\":{\"time\":1584864023,\"summary\":\"Possible Drizzle\",\"icon\":\"rain\",\"precipIntensity\":0.2386,\"precipProbability\":0.4,\"precipType\":\"rain\",\"temperature\":9.39,\"apparentTemperature\":8.3,\"dewPoint\":9.39,\"humidity\":1,\"pressure\":1010.8,\"windSpeed\":2.22,\"windGust\":3.63,\"windBearing\":71,\"cloudCover\":0.52,\"uvIndex\":1,\"visibility\":16.093,\"ozone\":391.9},\"offset\":1}";
            handler.SetupAnyRequest()
                .ReturnsResponse(darkSkyResponse, "application/json");

            var client = new WeatherService(
                loggerMock.Object,
                handler.CreateClient(),
                new ServiceHost.Entity.WeatherConfiguration() 
                { 
                    DarkSkyUrlTemplate = "https://api.darksky.net/forecast/{0}/{1},{2}?exclude=minutely,hourly,daily,flags,alerts&units=si"
                },
                otMetricsMock.Object);

            // Act
            //
            var result = await client.RequestDarkSky(lat, lon, token);

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal("rain", result.Icon);
            Assert.Equal((decimal)0.4, result.PrecipProbability);
            Assert.Equal((decimal)9.39, result.Temperature);
        }
    }
}
