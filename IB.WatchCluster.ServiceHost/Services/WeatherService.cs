using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace IB.WatchCluster.ServiceHost.Services
{
    public class WeatherService : IRequestHandler<WeatherInfo>
    {
        private readonly ILogger<WeatherService> _logger;
        private readonly HttpClient _httpClient;
        private readonly WeatherConfiguration _weatherConfig;
        private readonly OtMetrics _metrics;

        public WeatherService(
            ILogger<WeatherService> logger,HttpClient httpClient, WeatherConfiguration weatherConfig, OtMetrics metrics)
        {
            _logger = logger;
            _httpClient = httpClient;
            _weatherConfig = weatherConfig;
            _metrics = metrics;
        }

        public WeatherInfo Process(WatchRequest? watchRequest)
        {
            return ProcessAsync(watchRequest).Result;
        }

        public Task<WeatherInfo> ProcessAsync(WatchRequest? watchRequest)
        {
            if (watchRequest == null || !watchRequest.Lat.HasValue || !watchRequest.Lon.HasValue)
                return Task.FromResult(new WeatherInfo());

            Enum.TryParse<WeatherProvider>(watchRequest.WeatherProvider, true, out var weatherProvider);

            return (weatherProvider == WeatherProvider.DarkSky)
                ? RequestDarkSky(watchRequest.Lat.Value, watchRequest.Lon.Value, watchRequest.DarkskyKey)
                : RequestOpenWeather(watchRequest.Lat.Value, watchRequest.Lon.Value);
        }

        /// <summary>
        /// Request weather info on DarkSky weather provider
        /// </summary>
        /// <param name="lat">Latitude</param>
        /// <param name="lon">Longitude</param>
        /// <param name="token">ApiToken</param>
        /// <returns>Weather info <see cref="RequestDarkSky"/></returns>
        public async Task<WeatherInfo> RequestDarkSky(decimal lat, decimal lon, string token)
        {
            string providerName = WeatherProvider.DarkSky.ToString();
            _metrics.WeatherGetDarkSky.Add(1);

            var url = string.Format(_weatherConfig.DarkSkyUrlTemplate, token, lat, lon);
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                    ? $"Unauthorized access to {providerName}"
                    : $"Error {providerName} request, status: {response.StatusCode}");
                return new WeatherInfo { RequestStatus = new RequestStatus(response.StatusCode) };
            }

            await using var content = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(content);
            var weatherInfo = JsonSerializer.Deserialize<WeatherInfo>(
                json.RootElement.GetProperty("currently").GetRawText());
            if (weatherInfo == null)
                return new WeatherInfo { RequestStatus = new RequestStatus(RequestStatusCode.Error) };
            weatherInfo.WeatherProvider = providerName;
            weatherInfo.RequestStatus = new RequestStatus(RequestStatusCode.Ok);

            return weatherInfo;
        }

        /// <summary>
        /// Request weather conditions from OpenWeather service
        /// </summary>
        /// <param name="lat">latitude</param>
        /// <param name="lon">longitude</param>
        /// <returns>Weather conditions for the specified coordinates </returns>
        public async Task<WeatherInfo> RequestOpenWeather(decimal lat, decimal lon)
        {
            var providerName = WeatherProvider.OpenWeather.ToString();
            _metrics.WeatherGetOpenWeather.Add(1);

            var conditionIcons = new Dictionary<string, string>
            {
                {"01d", "clear-day"}, {"01n", "clear-night"},
                {"10d", "rain"}, {"10n", "rain"}, {"09d", "rain"}, {"09n", "rain"},  {"11d", "rain"}, {"11n", "rain"},
                {"13d", "snow"}, {"13n", "snow"},
                {"50d", "fog"}, {"50n", "fog"},
                {"03d","cloudy"}, {"03n","cloudy"},
                {"02d", "partly-cloudy-day"}, {"02n", "partly-cloudy-night"}, {"04d", "partly-cloudy-day"}, {"04n", "partly-cloudy-night"}
            };

            var url = string.Format(_weatherConfig.OpenWeatherUrlTemplate, lat, lon, _weatherConfig.OpenWeatherKey);
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(response.StatusCode == HttpStatusCode.Unauthorized
                    ? $"Unauthorized access to {providerName}"
                    : $"Error {providerName} request, status: {response.StatusCode}");
                return new WeatherInfo { RequestStatus = new RequestStatus(response.StatusCode) };
            }

            await using var content = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(content);

            var elements = json.RootElement.EnumerateObject()
                .Where(e => e.Name == "main" || e.Name == "weather" || e.Name == "wind")
                .SelectMany(e => (e.Value.ValueKind == JsonValueKind.Array ? e.Value[0] : e.Value).EnumerateObject())
                .Where(e => e.Name == "temp" || e.Name == "humidity" || e.Name == "pressure" || e.Name == "speed" || e.Name == "icon")
                .ToDictionary(e => e.Name, v => v.Name == "icon"
                    ? (object)(conditionIcons.ContainsKey(v.Value.GetString() ?? "") ? conditionIcons[v.Value.GetString() ?? ""] : "clear-day")
                    : v.Value.GetDecimal());

            var weatherInfo = new WeatherInfo
            {
                Humidity = (decimal)(elements["humidity"] ?? 0) / 100,
                Icon = elements["icon"].ToString(),
                Pressure = (decimal)(elements["pressure"] ?? 0),
                Temperature = (decimal)(elements["temp"] ?? 0),
                WindSpeed = (decimal)(elements["speed"] ?? 0),
                WeatherProvider = providerName,
                RequestStatus = new RequestStatus(RequestStatusCode.Ok)
            };
            return weatherInfo;
        }
    }
}
