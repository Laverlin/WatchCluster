using System.Text.Json;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using Microsoft.Extensions.Logging;

namespace IB.WatchCluster.ServiceHost.Services.CurrencyExchange;

public class CurrencyConverter: ExchangeRateBase
{
    public CurrencyConverter(
        ILogger<ExchangeRateBase> logger, CurrencyExchangeConfiguration exchangeConfig, HttpClient httpClient) 
        : base(logger, exchangeConfig, httpClient)
    { }

    protected override Uri CreatUrl(CurrencyExchangeConfiguration exchangeConfig, string baseCurrency, string targetCurrency)
    {
        return new Uri(
            string.Format(
                exchangeConfig.CurrencyConverterUrlTemplate, exchangeConfig.CurrencyConverterKey, baseCurrency, targetCurrency));
    }

    protected override ExchangeRateInfo ParseJson(JsonDocument jsonDocument, string baseCurrency = "", string targetCurrency = "")
    {
        return new ExchangeRateInfo
        {
            ExchangeRate = jsonDocument.RootElement.TryGetProperty($"{baseCurrency}_{targetCurrency}", out var rate)
                ? rate.GetDecimal() : 0,
            RequestStatus = new RequestStatus(RequestStatusCode.Ok),
            RemoteSource = "CurrencyConverter"
        };
    }
}