﻿using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Wrap;
using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.ServiceHost.Services.CurrencyExchange;

namespace IB.WatchCluster.ServiceHost.Services;

public class CurrencyExchangeService : IRequestHandler<ExchangeRateInfo>
{
    private static readonly MemoryCache MemoryCache = new (new MemoryCacheOptions());
    private readonly ILogger<CurrencyExchangeService> _logger;
    private readonly OtelMetrics _metrics;
    private readonly TwelveData _primarySource;
    private readonly AsyncPolicyWrap<ExchangeRateInfo> _fallbackPolicy;

    public CurrencyExchangeService (
        ILogger<CurrencyExchangeService> logger,
        OtelMetrics metrics,
        TwelveData primarySource,
        ExchangeHost backupSource)
    {
        _logger = logger;
        _metrics = metrics;
        _primarySource = primarySource;
        _fallbackPolicy = CreateRequestPolicy(backupSource.GetExchangeRateAsync);
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
                await _primarySource.GetExchangeRateAsync(watchRequest.BaseCurrency, watchRequest.TargetCurrency),
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