using System.Text.Json;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using Microsoft.Extensions.Logging;

namespace IB.WatchCluster.ServiceHost.Services.CurrencyExchange;

public class ExchangeHost : ExchangeRateBase
{
    public ExchangeHost(ILogger<ExchangeRateBase> logger, CurrencyExchangeConfiguration exchangeConfig, HttpClient httpClient) 
        : base(logger, exchangeConfig, httpClient)
    { }

    protected override Uri CreatUrl(CurrencyExchangeConfiguration exchangeConfig, string baseCurrency, string targetCurrency)
    {
        return new Uri(string.Format(exchangeConfig.ExchangeHostUrlTemplate, baseCurrency, targetCurrency));
    }

    protected override ExchangeRateInfo ParseJson(JsonDocument jsonDocument, string baseCurrency = "", string targetCurrency = "")
    {
        if (jsonDocument.RootElement.TryGetProperty("info", out var info))
            return new ExchangeRateInfo
            {
                ExchangeRate = info.TryGetProperty($"rate", out var rate)
                    ? rate.GetDecimal() : 0,
                RequestStatus = new RequestStatus(RequestStatusCode.Ok),
                RemoteSource = "ExchangeHost"
            };
        return new ExchangeRateInfo
        {
            ExchangeRate = 0,
            RequestStatus = new RequestStatus(RequestStatusCode.Error)
        };
    }
}