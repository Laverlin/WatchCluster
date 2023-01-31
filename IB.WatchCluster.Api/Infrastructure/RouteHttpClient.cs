using System.Diagnostics;
using System.Net;
using IB.WatchCluster.Abstract.Entity.SailingApp;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;


namespace IB.WatchCluster.Api.Infrastructure;

public class RouteHttpClient
{
    private readonly ILogger<RouteHttpClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly ActivitySource _activitySource;
    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public RouteHttpClient(ILogger<RouteHttpClient> logger, HttpClient httpClient, ActivitySource activitySource)
    {
        _logger = logger;
        _httpClient = httpClient;
        _activitySource = activitySource;
    }
    
    /// <summary>
    /// Returns list of routes
    /// </summary>
    /// <param name="puid">Public User ID</param>
    /// <returns></returns>
    /// <exception cref="ApiException"></exception>
    public async Task<YasRoute[]> GetRoutes(string puid)
    {
        using var activity = _activitySource.StartActivity("/routes", ActivityKind.Producer)!;
        var message = new HttpRequestMessage(HttpMethod.Get, $"/route-store/users/{puid}/routes");
        Propagator.Inject(
            new PropagationContext(activity.Context, Baggage.Current),
            message, (requestMessage, key, value) => requestMessage.Headers.Add(key, value)
        );

        using var getResponse = await _httpClient.SendAsync(message);
        if (getResponse.StatusCode == HttpStatusCode.NotFound)
            throw new ApiException(StatusCodes.Status404NotFound, "User not found");
        
        if (!getResponse.IsSuccessStatusCode)
            HandleError();

        var routes = await getResponse.Content.ReadFromJsonAsync<YasRoute[]>();
        if (routes == null)
            HandleError();
        
        void HandleError()
        {
            _logger.LogWarning("Unable get route list {@statusCode}, {@content}", 
                getResponse.StatusCode, getResponse.Content.ReadAsStringAsync().Result);
            throw new ApiException(StatusCodes.Status400BadRequest, "Bad request");
        }

        return routes!;

        void SetTraceHeaders(HttpRequestMessage message, string key, string value)
        {
            message.Headers.Add(key, value);
        }
    }
}