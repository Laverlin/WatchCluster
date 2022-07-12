using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Fallback;
using Polly.Wrap;
using System.Net;
using System.Text.Json;

namespace IB.WatchCluster.ServiceHost.Services
{
    public class CurrencyExchangeService : IRequestHandler<ExchangeRateInfo>
    {
        private static readonly MemoryCache _memoryCache = new (new MemoryCacheOptions());
        private readonly ILogger<CurrencyExchangeService> _logger;
        private readonly HttpClient _httpClient;
        private readonly CurrencyExchangeConfiguration _exchangeConfig;
        private readonly OtMetrics _metrics;
        private readonly AsyncPolicyWrap<ExchangeRateInfo> _fallbackPolicy;

        public CurrencyExchangeService (
            ILogger<CurrencyExchangeService> logger, HttpClient httpClient, CurrencyExchangeConfiguration exchangeConfig, OtMetrics metrics)
        {
            _logger = logger;
            _httpClient = httpClient;
            _exchangeConfig = exchangeConfig;
            _metrics = metrics;
            _fallbackPolicy = CreateRequestPolicy(RequestExchangeHost);
        }

        public ExchangeRateInfo Process(WatchRequest? watchRequest)
        {
            return ProcessAsync(watchRequest).Result;
        }

        public async Task<ExchangeRateInfo> ProcessAsync(WatchRequest? watchRequest)
        {
            if (watchRequest == null ||
                string.IsNullOrEmpty(watchRequest.BaseCurrency) || 
                string.IsNullOrEmpty(watchRequest.TargetCurrency) ||
                watchRequest.BaseCurrency == watchRequest.TargetCurrency)
                return new ExchangeRateInfo();

            string cacheKey = $"er-{watchRequest.BaseCurrency}-{watchRequest.TargetCurrency}";
            if (_memoryCache.TryGetValue(cacheKey, out ExchangeRateInfo exchangeRateInfo))
            {
                _metrics.ExchangeRateGetCached.Add(1);
                return exchangeRateInfo;
            }
            /*
            var fallbackPolicy = Policy<ExchangeRateInfo>
                .Handle<Exception>()
                .OrResult(_ => _.RequestStatus.StatusCode == RequestStatusCode.Error)
                .FallbackAsync(async cancellationToken =>
                {
                    return await RequestExchangeHost(watchRequest.BaseCurrency, watchRequest.TargetCurrency);
                }, onFallbackAsync: async _ =>
                {
                    _logger.LogWarning(_.Exception, "Fallback, object state {@ExchangeRateInfo}", _.Result);
                    await Task.CompletedTask.ConfigureAwait(false);
                });
            */
            exchangeRateInfo = await _fallbackPolicy.ExecuteAsync(async _ =>
                await RequestCurrencyConverter(watchRequest.BaseCurrency, watchRequest.TargetCurrency),
                new Dictionary<string, object> { { nameof(WatchRequest), watchRequest } });

            if (exchangeRateInfo.RequestStatus.StatusCode == RequestStatusCode.Ok && exchangeRateInfo.ExchangeRate != 0)
                _memoryCache.Set(cacheKey, exchangeRateInfo, TimeSpan.FromMinutes(60));

            return exchangeRateInfo;
        }

        public async Task<ExchangeRateInfo> RequestExchangeHost(string baseCurrency, string targetCurrency)
        {
            try
            {
                _metrics.ExchangeRateGetExchangeHost.Add(1);
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
                        RequestStatus = new RequestStatus(RequestStatusCode.Ok)
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
        /// <returns>exchange rate. if conversion is unsuccessfull the rate could be 0 </returns>
        public async Task<ExchangeRateInfo> RequestCurrencyConverter(string baseCurrency, string targetCurrency)
        {
            _metrics.ExchangeRateGetCurrencyConverter.Add(1);

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
                RequestStatus = new RequestStatus(RequestStatusCode.Ok)
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
                    fallbackAction: async (context, cancellationToken) =>
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
}
