using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace IB.WatchCluster.YasTelegramBot.Service;

public class YasHttpClient
{
    private readonly ILogger<YasHttpClient> _logger;
    private readonly HttpClient _httpClient;
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public YasHttpClient(ILogger<YasHttpClient> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public Task<HttpResponseMessage> GetRouteList(string token) => 
        RequestYasRestApi(HttpMethod.Get, $"route-store/users/{token}/routes");
    
    public Task<HttpResponseMessage> GetUser(long telegramId) => 
        RequestYasRestApi(HttpMethod.Get, $"user-store/users/{telegramId}");
    
    private Task<HttpResponseMessage> RequestYasRestApi(HttpMethod httpMethod, string url)
    {
        var requestMessage = new HttpRequestMessage(httpMethod, url);
        Propagator.Inject(
            new PropagationContext(Activity.Current?.Context ?? new ActivityContext(), Baggage.Current),
            requestMessage, (message, key, value) => message.Headers.Add(key, value));
        return _httpClient.SendAsync(requestMessage);
    }
}