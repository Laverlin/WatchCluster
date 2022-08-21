using System.Text.Json;
using Microsoft.Extensions.Logging;
using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.ServiceHost.Entity;
using IB.WatchCluster.ServiceHost.Infrastructure;

namespace IB.WatchCluster.ServiceHost.Services;

public class WeatherService : IRequestHandler<WeatherInfo>
{
    private readonly ILogger<WeatherService> _logger;
    private readonly HttpClient _httpClient;
    private readonly WeatherConfiguration _weatherConfig;
    private readonly OtelMetrics _metrics;

    public WeatherService(
        ILogger<WeatherService> logger,HttpClient httpClient, WeatherConfiguration weatherConfig, OtelMetrics metrics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _weatherConfig = weatherConfig;
        _metrics = metrics;
    }

    public async Task<WeatherInfo> ProcessAsync(WatchRequest? watchRequest)
    {
        var sourceKind = DataSourceKind.Empty;
        var weatherInfo = new WeatherInfo();
        var weatherProvider = WeatherProvider.OpenWeather;
        try
        {
            if (watchRequest is not { Lat: { }, Lon: { } })
                return weatherInfo;

            Enum.TryParse(watchRequest.WeatherProvider, true, out weatherProvider);

            weatherInfo = weatherProvider == WeatherProvider.DarkSky
                ? await RequestDarkSky(watchRequest.Lat.Value, watchRequest.Lon.Value, watchRequest.DarkskyKey)
                : await RequestOpenWeather(watchRequest.Lat.Value, watchRequest.Lon.Value);
            sourceKind = DataSourceKind.Remote;
            return weatherInfo;
        }
        finally
        {
            _metrics.IncreaseProcessedCounter(sourceKind, weatherInfo.RequestStatus.StatusCode, weatherProvider.ToString());
        }
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
        var providerName = WeatherProvider.DarkSky.ToString();
        var url = string.Format(_weatherConfig.DarkSkyUrlTemplate, token, lat, lon);
        
        using var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error {@provider} request, status: {@statusCode}", providerName, response.StatusCode);
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
            _logger.LogWarning("Error {@provider} request, status: {@statusCode}", providerName, response.StatusCode);
            return new WeatherInfo { RequestStatus = new RequestStatus(response.StatusCode) };
        }

        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);

        var elements = json.RootElement.EnumerateObject()
            .Where(e => e.Name is "main" or "weather" or "wind")
            .SelectMany(e => (e.Value.ValueKind == JsonValueKind.Array ? e.Value[0] : e.Value).EnumerateObject())
            .Where(e => e.Name is "temp" or "humidity" or "pressure" or "speed" or "icon")
            .ToDictionary(e => e.Name, v => v.Name == "icon"
                ? (object)(conditionIcons.ContainsKey(v.Value.GetString() ?? "") ? conditionIcons[v.Value.GetString() ?? ""] : "clear-day")
                : v.Value.GetDecimal());

        var weatherInfo = new WeatherInfo
        {
            Humidity = (decimal)elements["humidity"] / 100,
            Icon = elements["icon"].ToString(),
            Pressure = (decimal)elements["pressure"],
            Temperature = (decimal)elements["temp"],
            WindSpeed = (decimal)elements["speed"],
            WeatherProvider = providerName,
            RequestStatus = new RequestStatus(RequestStatusCode.Ok)
        };
        return weatherInfo;
    }
}