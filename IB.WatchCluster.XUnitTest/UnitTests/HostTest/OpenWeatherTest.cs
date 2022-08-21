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
    public class OpenWeatherTest
    {
        [Fact]
        public async Task OnSuccessShouldReturnValidObject()
        {
            // Arrange
            //
            var lat = (decimal)38.855652;
            var lon = (decimal)-94.799712;

            var otMetricsMock = new Mock<OtelMetrics>("", "", "");
            var loggerMock = new Mock<ILogger<WeatherService>>();

            var handler = new Mock<HttpMessageHandler>();
            var openWeatherResponse =
                "{\"coord\":{\"lon\":-94.8,\"lat\":38.88},\"weather\":[{\"id\":800,\"main\":\"Clear\",\"description\":\"clear sky\",\"icon\":\"01d\"}],\"base\":\"stations\",\"main\":{\"temp\":4.28,\"feels_like\":0.13,\"temp_min\":3,\"temp_max\":5.56,\"pressure\":1034,\"humidity\":51},\"visibility\":16093,\"wind\":{\"speed\":2.21,\"deg\":169},\"clouds\":{\"all\":1},\"dt\":1584811457,\"sys\":{\"type\":1,\"id\":5188,\"country\":\"US\",\"sunrise\":1584793213,\"sunset\":1584837126},\"timezone\":-18000,\"id\":4276614,\"name\":\"Olathe\",\"cod\":200}";
            handler.SetupAnyRequest()
                .ReturnsResponse(openWeatherResponse, "application/json");

            var client = new WeatherService(
                loggerMock.Object,
                handler.CreateClient(),
                new ServiceHost.Entity.WeatherConfiguration 
                { 
                    OpenWeatherUrlTemplate = "https://api.openweathermap.org/data/2.5/weather?lat={0}&lon={1}&units=metric&appid={2}"
                },
                otMetricsMock.Object);

            // Act
            //
            var result = await client.RequestOpenWeather(lat, lon);

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal("clear-day", result.Icon);
            Assert.Equal((decimal)0.51, result.Humidity);
            Assert.Equal((decimal)4.28, result.Temperature);
            Assert.Equal((decimal)2.21, result.WindSpeed);
        }
    }
}
