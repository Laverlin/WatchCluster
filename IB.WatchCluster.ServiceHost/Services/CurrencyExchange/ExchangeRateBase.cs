using System.Text.Json;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using Microsoft.Extensions.Logging;

namespace IB.WatchCluster.ServiceHost.Services.CurrencyExchange;

public abstract class ExchangeRateBase
{

    private readonly CurrencyExchangeConfiguration _exchangeConfig;
    private readonly HttpClient _httpClient;
    protected ILogger<ExchangeRateBase> Logger { get; }
    protected ExchangeRateBase(
        ILogger<ExchangeRateBase> logger, CurrencyExchangeConfiguration exchangeConfig, HttpClient httpClient)
    {
        Logger = logger;
        _exchangeConfig = exchangeConfig;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Override to return proper url
    /// </summary>
    /// <returns>Uri to connect</returns>
    protected abstract Uri CreatUrl(CurrencyExchangeConfiguration exchangeConfig, string baseCurrency, string targetCurrency);
    
    /// <summary>
    /// Override to extract data from Json
    /// </summary>
    protected abstract ExchangeRateInfo ParseJson(JsonDocument jsonDocument, string baseCurrency = "", string targetCurrency = "");
    
    /// <summary>
    /// Request rates from external source
    /// </summary>
    public virtual async Task<ExchangeRateInfo> GetExchangeRateAsync(string baseCurrency, string targetCurrency)
    {
        try
        {
            var url = CreatUrl(_exchangeConfig, baseCurrency, targetCurrency);
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogWarning("Error {@url} request, status:{@status}", url.Host, response.StatusCode);
                return new ExchangeRateInfo { RequestStatus = new RequestStatus(response.StatusCode) };
            }

            await using var content = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(content);
            var exchangeRate = ParseJson(json);
            return exchangeRate;
        }
        catch (Exception exception)
        {
            Logger.LogError("Error request: {@message}", exception.Message);
            return new ExchangeRateInfo { RequestStatus = new RequestStatus(RequestStatusCode.Error) };
        }
    }
}