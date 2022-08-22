using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;
using System.Net;
using System.Text.Json;
using IB.WatchCluster.Abstract.Entity;

namespace IB.WatchCluster.ServiceHost.Services;

public class CurrencyExchangeService : IRequestHandler<ExchangeRateInfo>
{
    private static readonly MemoryCache MemoryCache = new (new MemoryCacheOptions());
    private readonly ILogger<CurrencyExchangeService> _logger;
    private readonly HttpClient _httpClient;
    private readonly CurrencyExchangeConfiguration _exchangeConfig;
    private readonly OtelMetrics _metrics;
    private readonly AsyncPolicyWrap<ExchangeRateInfo> _fallbackPolicy;

    public CurrencyExchangeService (
        ILogger<CurrencyExchangeService> logger, 
        HttpClient httpClient, 
        CurrencyExchangeConfiguration exchangeConfig, 
        OtelMetrics metrics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _exchangeConfig = exchangeConfig;
        _metrics = metrics;
        _fallbackPolicy = CreateRequestPolicy(RequestExchangeHost);
    }
    
    public async Task<ExchangeRateInfo> ProcessAsync(WatchRequest? watchRequest)
    {
        var sourceKind = DataSourceKind.Empty;
        ExchangeRateInfo exchangeRateInfo = new ();
        try
        {
            if (watchRequest == null ||
                string.IsNullOrEmpty(watchRequest.BaseCurrency) ||
                string.IsNullOrEmpty(watchRequest.TargetCurrency) ||
                watchRequest.BaseCurrency == watchRequest.TargetCurrency)
                return exchangeRateInfo;

            var cacheKey = $"er-{watchRequest.BaseCurrency}-{watchRequest.TargetCurrency}";
            if (MemoryCache.TryGetValue(cacheKey, out exchangeRateInfo))
            {
                sourceKind = DataSourceKind.Cache;
                return exchangeRateInfo;
            }

            exchangeRateInfo = await _fallbackPolicy.ExecuteAsync(async _ =>
                await RequestTwelveData(watchRequest.BaseCurrency, watchRequest.TargetCurrency),
                    // await RequestExchangeHost(watchRequest.BaseCurrency, watchRequest.TargetCurrency),
                    //await RequestCurrencyConverter(watchRequest.BaseCurrency, watchRequest.TargetCurrency),
                new Dictionary<string, object> { { nameof(WatchRequest), watchRequest } });

            if (exchangeRateInfo.RequestStatus.StatusCode == RequestStatusCode.Ok && exchangeRateInfo.ExchangeRate != 0)
            {
                MemoryCache.Set(cacheKey, exchangeRateInfo, TimeSpan.FromMinutes(60));
                _metrics.SetMemoryCacheGauge(MemoryCache.Count);
            }
            
            sourceKind = DataSourceKind.Remote;
            return exchangeRateInfo;
        }
        finally
        {
            _metrics.IncreaseProcessedCounter(
                sourceKind, exchangeRateInfo.RequestStatus.StatusCode, exchangeRateInfo.RemoteSource,
                sourceKind == DataSourceKind.Empty
                    ? Enumerable.Empty<KeyValuePair<string, object?>>()
                    : new [] 
                    {
                        new KeyValuePair<string, object?>("pair", $"{watchRequest?.BaseCurrency}:{watchRequest?.TargetCurrency}") 
                    });
        }
    }

    public async Task<ExchangeRateInfo> RequestExchangeHost(string baseCurrency, string targetCurrency)
    {
        try
        {
            var url = string.Format(_exchangeConfig.ExchangeHostUrlTemplate, baseCurrency, targetCurrency);
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Error ExchangeHost request, status:{@status}", response.StatusCode);
                return new ExchangeRateInfo { RequestStatus = new RequestStatus(response.StatusCode) };
            }

            await using var content = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(content);
            if (json.RootElement.TryGetProperty("info", out var info))
                return new ExchangeRateInfo
                {
                    ExchangeRate = info.TryGetProperty($"rate", out var rate)
                        ? rate.GetDecimal() : 0,
                    RequestStatus = new RequestStatus(RequestStatusCode.Ok),
                    RemoteSource = "ExchangeHost"
                };
            else
                return new ExchangeRateInfo
                {
                    ExchangeRate = 0,
                    RequestStatus = new RequestStatus(RequestStatusCode.Error)
                };
        }
        catch (Exception exception)
        {
            _logger.LogError("Error ExchangeHost request: {@message}", exception.Message);
            return new ExchangeRateInfo { RequestStatus = new RequestStatus(RequestStatusCode.Error) };
        }
    }

    /// <summary>
    /// Request current Exchange rate on  currencyconverterapi.com
    /// </summary>
    /// <param name="baseCurrency">the currency from which convert</param>
    /// <param name="targetCurrency">the currency to which convert</param>
    /// <returns>exchange rate. if conversion is unsuccessful the rate could be 0 </returns>
    public async Task<ExchangeRateInfo> RequestCurrencyConverter(string baseCurrency, string targetCurrency)
    {
        var url = string.Format(_exchangeConfig.CurrencyConverterUrlTemplate, _exchangeConfig.CurrencyConverterKey, baseCurrency, targetCurrency);
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                ? "Unauthorized access to currencyconverterapi.com"
                : $"Error currencyconverterapi.com request, status: {response.StatusCode}");
            return new ExchangeRateInfo { RequestStatus = new RequestStatus(response.StatusCode) };
        }

        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);

        return new ExchangeRateInfo
        {
            ExchangeRate = json.RootElement.TryGetProperty($"{baseCurrency}_{targetCurrency}", out var rate)
                ? rate.GetDecimal() : 0,
            RequestStatus = new RequestStatus(RequestStatusCode.Ok),
            RemoteSource = "CurrencyConverter"
        };
    }
    
    public async Task<ExchangeRateInfo> RequestTwelveData(string baseCurrency, string targetCurrency)
    {
        var url = string.Format(
            _exchangeConfig.TwelveDataUrlTemplate, _exchangeConfig.TwelveDataKey, baseCurrency, targetCurrency);
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                ? "Unauthorized access to TwelveData.com"
                : $"Error TwelveData.com request, status: {response.StatusCode}");
            return new ExchangeRateInfo { RequestStatus = new RequestStatus(response.StatusCode) };
        }

        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);

        return new ExchangeRateInfo
        {
            ExchangeRate = json.RootElement.TryGetProperty("rate", out var rate) ? rate.GetDecimal() : 0,
            RequestStatus = new RequestStatus(RequestStatusCode.Ok),
            RemoteSource = "TwelveData"
        };
    }

    private AsyncPolicyWrap<ExchangeRateInfo> CreateRequestPolicy(Func<string, string, Task<ExchangeRateInfo>> fallbackRequest)
    {
        var circuitBreaker = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 2,
                durationOfBreak: TimeSpan.FromHours(10),
                onBreak: (exception, _) => _logger.LogWarning(exception, "Circuit is broken, go direct to fallback"),
                onReset: () => _logger.LogWarning("Back to normal"));

        return Policy<ExchangeRateInfo>
            .Handle<Exception>()
            .OrResult(result => result.RequestStatus.StatusCode == RequestStatusCode.Error)
            .FallbackAsync(
                fallbackAction: async (context, _) =>
                {
                    var watchRequest = (WatchRequest)context[nameof(WatchRequest)];
                    return await fallbackRequest(watchRequest.BaseCurrency, watchRequest.TargetCurrency);
                },
                onFallbackAsync: async (result, context) =>
                {
                    _logger.LogWarning(
                        result.Exception, 
                        "Fallback, object state: {@ExchangeRateInfo}, params: {@params}", 
                        result.Result, 
                        (context[nameof(WatchRequest)] as WatchRequest)?.BaseCurrency + "-" +
                        (context[nameof(WatchRequest)] as WatchRequest)?.TargetCurrency);
                    await Task.CompletedTask;
                })
            .WrapAsync(circuitBreaker);
    }
}