using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using IB.WatchCluster.Abstract.Entity;

namespace IB.WatchCluster.ServiceHost.Services;

public class VirtualEarthService : IRequestHandler<LocationInfo>
{
    private readonly ILogger<VirtualEarthService> _logger;
    private readonly HttpClient _httpClient;
    private readonly VirtualEarthConfiguration _virtualEarthConfig;
    private readonly OtelMetrics _metrics;
    private static readonly MemoryCache MemoryCache = new (new MemoryCacheOptions());

    /// <summary>
    /// Need to store lat/lon in cache
    /// </summary>
    private class LocationCache
    {
        public string LocationName { get; init; } = default!;
        public decimal Latitude { get; init; }
        public decimal Longitude { get; init; }
    }

    /// <summary>
    /// Need to store the source of data internally for statistics and monitoring
    /// </summary>
    private class LocationInfoInternal
    {
        public LocationInfo LocationInfo { get; init; } = null!;

        public bool IsFromCache { get; init; }
    }

    /// <summary>
    /// Service class to get location data
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="virtualEarthConfig"></param>
    /// <param name="httpClient"></param>
    /// <param name="metrics"></param>
    public VirtualEarthService(
        ILogger<VirtualEarthService> logger,
        VirtualEarthConfiguration virtualEarthConfig,
        HttpClient httpClient,
        OtelMetrics metrics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _virtualEarthConfig = virtualEarthConfig;
        _metrics = metrics;
    }

    /// <summary>
    /// Get location data
    /// </summary>
    /// <param name="watchRequest">incoming parameters</param>
    public async Task<LocationInfo> ProcessAsync(WatchRequest? watchRequest)
    {
        var sourceKind = DataSourceKind.Empty;
        var locationInfo = new LocationInfo();
        try
        {
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
            _metrics.IncreaseProcessedCounter(sourceKind, locationInfo.RequestStatus.StatusCode, "VirtualEarth");
        }
    }

    /// <summary>
    /// Trying to get data from cache and request the real code if not
    /// </summary>
    /// <param name="deviceId"></param>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <returns></returns>
    private async Task<LocationInfoInternal> RequestLocationNameCached(string deviceId, decimal lat, decimal lon)
    {
        var cacheKey = $"loc-{deviceId}";
        if (MemoryCache.TryGetValue(cacheKey, out LocationCache locationCache))
        {
            if (locationCache.Latitude == lat && locationCache.Longitude == lon)
                return new LocationInfoInternal
                {
                    LocationInfo = new LocationInfo (locationCache.LocationName), 
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
    /// Request LocationName on VirtualEarth
    /// </summary>
    /// <param name="lat">Latitude</param>
    /// <param name="lon">Longitude</param>
    /// <returns>Location Name info <see cref="LocationInfo"/></returns>
    public async Task<LocationInfo> RequestLocationName(decimal lat, decimal lon)
    {
        var requestUrl = string.Format(
            _virtualEarthConfig.UrlTemplate, lat.ToString("G"), lon.ToString("G"), _virtualEarthConfig.AuthKey);
        using var response = await _httpClient.GetAsync(requestUrl);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                ? $"Unauthorized access to virtualearth"
                : $"Error virtualearth request, status: {response.StatusCode}");
            return new LocationInfo { RequestStatus = new RequestStatus(response.StatusCode) };
        }

        var content = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(content);
        var resource = document.RootElement
            .GetProperty("resourceSets")[0]
            .GetProperty("resources");

        if (resource.GetArrayLength() <= 0) return new LocationInfo();
        string locality;
        var region = "";
        var address = resource[0].GetProperty("address");
        if (address.TryGetProperty("locality", out var localityElement))
            locality = $"{localityElement.GetString()}, ";
        else
            locality = address.TryGetProperty("adminDistrict", out var districtElement)
                ? $"{districtElement.GetString()}, " : "";
        if (address.TryGetProperty("countryRegion", out var regionElement))
            region = regionElement.GetString();

        return new LocationInfo($"{locality}{region}");
    }
}