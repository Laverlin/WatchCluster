
using System;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Infrastructure;
using IB.WatchCluster.ServiceHost.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Threading.Tasks;
using IB.WatchCluster.ServiceHost.Services.CurrencyExchange;

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
            var otelMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var psMock = new Mock<TwelveData>(null, null, null);
            var fbMock = new Mock<ExchangeHost>(null, null, null);
            
            psMock
                .Setup(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new ExchangeRateInfo
                    { ExchangeRate = (decimal)51.440375, RequestStatus = new RequestStatus(RequestStatusCode.Ok)}))
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                otelMetricsMock.Object,
                psMock.Object,
                fbMock.Object);

            // Act
            //
            var result = await client.ProcessAsync(new WatchRequest{BaseCurrency = "EUR", TargetCurrency = "PHP"});

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)51.440375, result.ExchangeRate);
        }

       [Fact]
        public async Task SecondSuccessShouldReturnFromCache()
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
            var psMock = new Mock<TwelveData>(null, null, null);
            var fbMock = new Mock<ExchangeHost>(null, null, null);

            psMock
                .Setup(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new ExchangeRateInfo
                    { ExchangeRate = (decimal)51.440375, RequestStatus = new RequestStatus(RequestStatusCode.Ok)}))
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                otelMetricsMock.Object,
                psMock.Object,
                fbMock.Object);

            // Act
            //
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "USD", TargetCurrency="PHP" });
            var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "USD", TargetCurrency = "PHP" });
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "EUR", TargetCurrency = "USD" });
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "RUR", TargetCurrency = "PHP" });

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)51.440375, result.ExchangeRate);
            psMock.Verify( t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        }

        [Fact]
        public async Task AfterFailureFallbackShouldWork()
        {
            // Arrange
            //
            var otelMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var psMock = new Mock<TwelveData>(null, null, null);
            var fbMock = new Mock<ExchangeHost>(null, null, null);

            psMock
                .Setup(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new ApplicationException())
                .Verifiable();
            fbMock
                .Setup(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new ExchangeRateInfo
                    { ExchangeRate = (decimal)51.440375, RequestStatus = new RequestStatus(RequestStatusCode.Ok)}))
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                otelMetricsMock.Object,
                psMock.Object,
                fbMock.Object);

            // Act
            //
            var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "EUR", TargetCurrency = "RUB" });

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)51.440375, result.ExchangeRate);
            psMock.Verify(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(1));
            fbMock.Verify(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(1));
        }

        [Fact]
        public async Task IfFailureTwiceCircuitShouldCallFallbackDirectly()
        {
            // Arrange
            //
            var otelMetricsMock = new Mock<OtelMetrics>("","","");
            var loggerMock = new Mock<ILogger<CurrencyExchangeService>>();
            var psMock = new Mock<TwelveData>(null, null, null);
            var fbMock = new Mock<ExchangeHost>(null, null, null);

            psMock
                .Setup(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new ApplicationException())
                .Verifiable();
            fbMock
                .Setup(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new ExchangeRateInfo
                    { ExchangeRate = (decimal)51.440375, RequestStatus = new RequestStatus(RequestStatusCode.Ok)}))
                .Verifiable();

            var client = new CurrencyExchangeService(
                loggerMock.Object,
                otelMetricsMock.Object,
                psMock.Object,
                fbMock.Object);
            

            // Act
            //
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "ZZZ", TargetCurrency = "AAA" });
            await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "YYY", TargetCurrency = "BBB" });
            var result = await client.ProcessAsync(new WatchRequest { DeviceId = "2", BaseCurrency = "AAA", TargetCurrency = "ZZZ" });

            // Assert
            //
            Assert.Equal(RequestStatusCode.Ok, result.RequestStatus.StatusCode);
            Assert.Equal((decimal)51.440375, result.ExchangeRate);
            psMock.Verify(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            fbMock.Verify(t=>t.GetExchangeRateAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(3));
        }
    }
}
