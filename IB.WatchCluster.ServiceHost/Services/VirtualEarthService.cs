using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

using System.Net;
using System.Text.Json;


namespace IB.WatchCluster.ServiceHost.Services
{
    public class VirtualEarthService : IRequestHandler<LocationInfo>
    {
        private readonly ILogger<VirtualEarthService> _logger;
        private readonly HttpClient _httpClient;
        private readonly VirtualEarthConfiguration _virtualEarthConfig;
        private readonly OtMetrics _metrics;
        private static readonly MemoryCache _memoryCache = new (new MemoryCacheOptions());

        private class LocationCache
        {
            public string LocationName { get; set; } = default!;
            public decimal Latitude { get; set; }
            public decimal Longitude { get; set; }
        }

        public VirtualEarthService(
            ILogger<VirtualEarthService> logger, HttpClient httpClient, VirtualEarthConfiguration virtualEarthConfig, OtMetrics metrics)
        {
            _logger = logger;
            _httpClient = httpClient;
            _virtualEarthConfig = virtualEarthConfig;
            _metrics = metrics;
        }

        public LocationInfo Process(WatchRequest? watchRequest)
        {
            return ProcessAsync(watchRequest).Result;
        }

        public Task<LocationInfo> ProcessAsync(WatchRequest? watchRequest)
        {
            if (watchRequest == null || !watchRequest.Lat.HasValue || !watchRequest.Lon.HasValue)
                return Task.FromResult(new LocationInfo());

            return RequestLocationNameCached(watchRequest.DeviceId, watchRequest.Lat.Value, watchRequest.Lon.Value);
        }

        public async Task<LocationInfo> RequestLocationNameCached(string deviceId, decimal lat, decimal lon)
        {
            string cacheKey = $"loc-{deviceId}";
            if (_memoryCache.TryGetValue(cacheKey, out LocationCache locationCache))
            {
                if (locationCache.Latitude == lat && locationCache.Longitude == lon)
                {
                    _metrics.LocationGetCached.Add(1);
                    return new LocationInfo(locationCache.LocationName);
                }

                _memoryCache.Remove(cacheKey);
            }

            var locationInfo = await RequestLocationName(lat, lon);
            _memoryCache.Set(
                cacheKey,
                new LocationCache { LocationName = locationInfo.CityName, Latitude = lat, Longitude = lon },
                TimeSpan.FromHours(24));

            return locationInfo;
        }

        /// <summary>
        /// Request LocationName on VirtualEarth
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <returns>Location Name info <see cref="LocationInfo"/></returns>
        public async Task<LocationInfo> RequestLocationName(decimal lat, decimal lon)
        {
            _metrics.LocationGetRemote.Add(1);

            var requestUrl = string.Format(_virtualEarthConfig.UrlTemplate, lat.ToString("G"), lon.ToString("G"), _virtualEarthConfig.AuthKey);
            using var response = await _httpClient.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                    ? $"Unauthorized access to virtualearth"
                    : $"Error virtualearth request, status: {response.StatusCode}");
                return new LocationInfo { RequestStatus = new RequestStatus(response.StatusCode) };
            }

            var content = await response.Content.ReadAsStreamAsync();
            using var document = JsonDocument.Parse(content);
            var resource = document.RootElement
                .GetProperty("resourceSets")[0]
                .GetProperty("resources");

            string location = "";
            if (resource.GetArrayLength() > 0)
            {
                var locality = "";
                var region = "";
                var address = resource[0].GetProperty("address");
                if (address.TryGetProperty("locality", out JsonElement localityElement))
                    locality = $"{localityElement.GetString()}, ";
                else
                    locality = (address.TryGetProperty("adminDistrict", out JsonElement districtElement))
                        ? locality = $"{districtElement.GetString()}, " : "";
                if (address.TryGetProperty("countryRegion", out JsonElement regionElement))
                    region = regionElement.GetString();
                location = $"{locality}{region}";
            }

            return new LocationInfo(location);
        }
    }
}
