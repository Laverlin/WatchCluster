using System.Diagnostics;
using System.Globalization;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using IB.WatchCluster.Abstract.Entity;

namespace IB.WatchCluster.ServiceHost.Services;

public class GeoapifyService : IRequestHandler<LocationInfo>
{
    private readonly ILogger<GeoapifyService> _logger;
    private readonly HttpClient _httpClient;
    private readonly GeoapifyConfiguration _geoapifyConfig;
    private readonly OtelMetrics _metrics;
    private static readonly MemoryCache MemoryCache = new(new MemoryCacheOptions());

    private class LocationCache
    {
        public string LocationName { get; init; } = default!;
        public decimal Latitude { get; init; }
        public decimal Longitude { get; init; }
    }

    private class LocationInfoInternal
    {
        public LocationInfo LocationInfo { get; init; } = null!;
        public bool IsFromCache { get; init; }
    }

    public GeoapifyService(
        ILogger<GeoapifyService> logger,
        GeoapifyConfiguration geoapifyConfiguration,
        HttpClient httpClient,
        OtelMetrics metrics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _geoapifyConfig = geoapifyConfiguration;
        _metrics = metrics;
    }

    public async Task<LocationInfo> ProcessAsync(WatchRequest? watchRequest)
    {
        var sourceKind = DataSourceKind.Empty;
        var locationInfo = new LocationInfo();
        Stopwatch processTimer = new();
        try
        {
            processTimer.Start();
            if (watchRequest is not { Lat: { }, Lon: { } })
                return locationInfo;

            var locationInfoInternal = await RequestLocationNameCached(
                watchRequest.DeviceId, watchRequest.Lat.Value, watchRequest.Lon.Value);
            locationInfo = locationInfoInternal.LocationInfo;
            sourceKind = locationInfoInternal.IsFromCache ? DataSourceKind.Cache : DataSourceKind.Remote;
            return locationInfo;
        }
        finally
        {
            processTimer.Stop();
            _metrics.SetProcessingDuration(
                processTimer.ElapsedMilliseconds, sourceKind, locationInfo.RequestStatus.StatusCode, "Geoapify");
            _metrics.IncreaseProcessedCounter(sourceKind, locationInfo.RequestStatus.StatusCode, "Geoapify");
        }
    }

    private async Task<LocationInfoInternal> RequestLocationNameCached(string deviceId, decimal lat, decimal lon)
    {
        var cacheKey = $"loc-{deviceId}";
        if (MemoryCache.TryGetValue(cacheKey, out LocationCache locationCache))
        {
            if (locationCache.Latitude == lat && locationCache.Longitude == lon)
                return new LocationInfoInternal
                {
                    LocationInfo = new LocationInfo(locationCache.LocationName),
                    IsFromCache = true
                };
            MemoryCache.Remove(cacheKey);
        }

        var locationInfo = await RequestLocationName(lat, lon);
        if (locationInfo.RequestStatus.StatusCode == RequestStatusCode.Ok)
            MemoryCache.Set(
                cacheKey,
                new LocationCache { LocationName = locationInfo.CityName, Latitude = lat, Longitude = lon },
                TimeSpan.FromHours(24));

        _metrics.SetMemoryCacheGauge(MemoryCache.Count);
        return new LocationInfoInternal { LocationInfo = locationInfo, IsFromCache = false };
    }

    /// <summary>
    /// Request LocationName on Geoapify
    /// </summary>
    /// <param name="lat">Latitude</param>
    /// <param name="lon">Longitude</param>
    /// <returns>Location Name info <see cref="LocationInfo"/></returns>
    public async Task<LocationInfo> RequestLocationName(decimal lat, decimal lon)
    {
        var requestUrl = string.Format(
            _geoapifyConfig.UrlTemplate,
            lon.ToString("G", CultureInfo.InvariantCulture),
            lat.ToString("G", CultureInfo.InvariantCulture),
            _geoapifyConfig.AuthKey);
        using var response = await _httpClient.GetAsync(requestUrl);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                ? $"Unauthorized access to Geoapify"
                : $"Error geoapify request, status: {response.StatusCode}");
            return new LocationInfo { RequestStatus = new RequestStatus(response.StatusCode) };
        }

        await using var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);
        var features = document.RootElement.GetProperty("features");

        if (features.GetArrayLength() == 0)
            return new LocationInfo { RequestStatus = new RequestStatus(RequestStatusCode.Ok) };

        var properties = features[0].GetProperty("properties");

        string locality = "";
        if (TryGetFirstString(properties, out var city,
                "city", "town", "village", "hamlet", "municipality", "suburb", "county", "state"))
        {
            locality = city + ", ";
        }

        string country = properties.TryGetProperty("country", out var countryEl) ? countryEl.GetString() : string.Empty;

        _logger.LogDebug($"Geoapify location: {locality}{country}");
        return new LocationInfo($"{locality}{country}");
    }

    private static bool TryGetFirstString(JsonElement props, out string value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (props.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String)
            {
                value = el.GetString();
                return true;
            }
        }
        value = string.Empty;
        return false;
    }
}
