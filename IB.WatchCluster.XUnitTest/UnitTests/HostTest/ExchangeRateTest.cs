
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
    public class ExchangeRateTest
    {
        [Fact]
        public async Task OnSuccessShouldReturnValidObject()
        {
            // Arrange
            //
            var config = new ServiceHost.Entity.CurrencyExchangeConfiguration
            {
                CurrencyConverterKey = "test_Key",
                CurrencyConverterUrlTemplate = "https://free.currconv.com/api/v7/convert?apiKey={0}&q={1}_{2}&compact=ultra"
            };
            var otelMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var handler = new Mock<HttpMessageHandler>();
            var ccResponse = "{\"EUR_PHP\": 51.440375}";

            handler
                .SetupAnyRequest()
                .ReturnsResponse(ccResponse, "application/json");

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                handler.CreateClient(),
                config,
                otelMetricsMock.Object);

            // Act
            //
            var result = await client.RequestCurrencyConverter("EUR", "PHP");

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)51.440375, result.ExchangeRate);
        }

       // [Fact]
        public async Task SecondSuccesShouldReturnFromCache()
        {
            // Arrange
            //
            var config = new ServiceHost.Entity.CurrencyExchangeConfiguration
            {
                CurrencyConverterKey = "test_Key",
                CurrencyConverterUrlTemplate = "https://free.currconv.com/api/v7/convert?apiKey={0}&q={1}_{2}&compact=ultra"
            };
            var otMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var handler = new Mock<HttpMessageHandler>();
            var ccResponse = "{\"EUR_PHP\": 51.440375}";

            handler
                .SetupAnyRequest()
                .ReturnsResponse(ccResponse, "application/json")
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                handler.CreateClient(),
                config,
                otMetricsMock.Object);

            // Act
            //
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "EUR", TargetCurrency="PHP" });
            var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "EUR", TargetCurrency = "PHP" });
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "EUR", TargetCurrency = "USD" });
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "RUR", TargetCurrency = "PHP" });

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)51.440375, result.ExchangeRate);
            handler.VerifyAnyRequest(Times.Exactly(3));
        }

        [Fact]
        public async Task AfterFailureFallbackShouldWork()
        {
            // Arrange
            //
            var config = new ServiceHost.Entity.CurrencyExchangeConfiguration
            {
                CurrencyConverterKey = "test_Key",
                CurrencyConverterUrlTemplate = "https://free.currconv.com/api/v7/convert?apiKey={0}&q={1}_{2}&compact=ultra",
                ExchangeHostUrlTemplate = "https://api.exchangerate.host/convert?from={0}&to={1}"

            };
            var otMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var handler = new Mock<HttpMessageHandler>();
            var ccResponse = "{\"info\": {\"rate\": 70.155903}}";

            var ccRequest = "https://free.currconv.com/api/v7/convert?apiKey=test_Key&q=EUR_RUB&compact=ultra";
            var ehRequest = "https://api.exchangerate.host/convert?from=EUR&to=RUB";

            handler
                .SetupRequest(HttpMethod.Get, ccRequest)
                .ReturnsResponse(System.Net.HttpStatusCode.BadRequest)
                .Verifiable();
            handler
                .SetupRequest(HttpMethod.Get, ehRequest)
                .ReturnsResponse(ccResponse, "application/json")
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                handler.CreateClient(),
                config,
                otMetricsMock.Object);

            // Act
            //
            var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "EUR", TargetCurrency = "RUB" });

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)70.155903, result.ExchangeRate);
//            handler.VerifyRequest(ccRequest, Times.Exactly(1));
            handler.VerifyRequest(ehRequest, Times.Exactly(1));
        }

       // [Fact]
        public async Task IfFailureTwiceCircuitShouldCallFallbackDirectly()
        {
            // Arrange
            //
            var config = new ServiceHost.Entity.CurrencyExchangeConfiguration
            {
                CurrencyConverterKey = "test_Key",
                CurrencyConverterUrlTemplate = "https://free.currconv.com/api/v7/convert?apiKey={0}&q={1}_{2}&compact=ultra",
                ExchangeHostUrlTemplate = "https://api.exchangerate.host/convert?from={0}&to={1}"

            };
            var otMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var handler = new Mock<HttpMessageHandler>();
            var ccResponse = "{\"info\": {\"rate\": 70.155903}}";

            //var ccRequest = "https://free.currconv.com/api/v7/convert?apiKey=test_Key&q=EUR_RUB&compact=ultra";
            //var ehRequest = "https://api.exchangerate.host/convert?from=AAA&to=ZZZ";
            
            var ehRequest = "https://free.currconv.com/api/v7/convert?apiKey=test_Key&q=EUR_RUB&compact=ultra";
            var ccRequest = "https://api.exchangerate.host/convert?from=AAA&to=ZZZ";

            handler
                .SetupRequest(HttpMethod.Get, ccRequest)
                .ReturnsResponse(System.Net.HttpStatusCode.BadRequest)
                .Verifiable();
            handler
                .SetupRequest(HttpMethod.Get, ehRequest)
                .ReturnsResponse(ccResponse, "application/json")
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                handler.CreateClient(),
                config,
                otMetricsMock.Object);

            // Act
            //
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "ZZZ", TargetCurrency = "AAA" });
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "YYY", TargetCurrency = "BBB" });
            var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "AAA", TargetCurrency = "ZZZ" });

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)70.155903, result.ExchangeRate);
            handler.VerifyRequest(r => r.RequestUri?.Host == "free.currconv.com", Times.Exactly(2));
            handler.VerifyRequest(r => r.RequestUri?.Host == "api.exchangerate.host", Times.Exactly(3));
        }
    }
}
