using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        ILogger<WeatherService> logger, HttpClient httpClient, WeatherConfiguration weatherConfig, OtelMetrics metrics)
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
        Stopwatch processTimer = new ();
        try
        {
            if (watchRequest is not { Lat: { }, Lon: { } })
                return weatherInfo;

            processTimer.Start();

            Enum.TryParse(watchRequest.WeatherProvider, true, out weatherProvider);

            weatherInfo = weatherProvider switch
            {
                WeatherProvider.DarkSky => await RequestDarkSky(watchRequest.Lat.Value, watchRequest.Lon.Value,
                    watchRequest.DarkskyKey),
                WeatherProvider.AppleDarkSky => await RequestAppleDarkSky(watchRequest.Lat.Value, watchRequest.Lon.Value),
                WeatherProvider.OpenWeather => await RequestOpenWeather(watchRequest.Lat.Value, watchRequest.Lon.Value),
                _ => new WeatherInfo { RequestStatus = new RequestStatus(RequestStatusCode.Error) }
            };

            sourceKind = DataSourceKind.Remote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,"Request weather exception, {@provider}", weatherProvider);
            weatherInfo.RequestStatus = new RequestStatus
            {
                ErrorCode = 500,
                ErrorDescription = "Internal Server Error",
                StatusCode = RequestStatusCode.Error
            };
        }
        finally
        {
            processTimer.Stop();
            _metrics.SetProcessingDuration(
                processTimer.ElapsedMilliseconds, sourceKind, weatherInfo.RequestStatus.StatusCode, weatherProvider.ToString());
            _metrics.IncreaseProcessedCounter(
                sourceKind, weatherInfo.RequestStatus.StatusCode, weatherProvider.ToString());
        }
        return weatherInfo;
    }
    
    
    public async Task<WeatherInfo> RequestAppleDarkSky(decimal lat, decimal lon)
    {
        var conditionIcons = new Dictionary<string, string>
        {
            { "Clear1", "clear-day"}, { "Clear0", "clear-night"},
            { "Cloudy", "cloudy"},
            { "Haze1", "fog" },
            { "MostlyClear1", "partly-cloudy-day"}, { "MostlyClear0", "partly-cloudy-night"},
            { "MostlyCloudy1", "partly-cloudy-day"}, { "MostlyCloudy0", "partly-cloudy-night"},
            { "PartlyCloudy1", "partly-cloudy-day"}, { "PartlyCloudy0", "partly-cloudy-night"},
            { "ScatteredThunderstorms","rain"},
            { "Breezy", "wind"},
            { "Windy", "wind"},
            { "Drizzle", "rain"},
            { "HeavyRain", "rain"},
            { "Rain", "rain"},
            { "Flurries", "snow"},
            { "HeavySnow", "snow"},
            { "Sleet", "sleet" },
            { "Snow", "snow"},
            { "Blizzard", "snow"},
            { "BlowingSnow", "snow"},
            { "FreezingDrizzle", "sleet"},
            { "FreezingRain", "sleet"},
            { "Frigid1","clear-day"}, { "Frigid0","clear-night"},
            { "Hail", "rain"},
            { "Hot1", "clear-day"}, { "Hot0","clear-night"},
            { "Hurricane", "wind"},
            { "IsolatedThunderstorms", "rain"},
            { "TropicalStorm", "rain"},
            { "BlowingDust", "wind"},
            { "Foggy", "fog"},
            { "Smoky", "fog" },
            { "StrongStorms", "rain"},
            { "SunFlurries", "snow"},
            { "SunShowers", "rain"},
            { "Thunderstorms", "rain"},
            { "WintryMix", "cloudy"}
        };
        
        var providerName = WeatherProvider.AppleDarkSky.ToString();
        var url = string.Format(
            _weatherConfig.AppleDarkSkyUrlTemplate, 
            lat.ToString(CultureInfo.InvariantCulture), 
            lon.ToString(CultureInfo.InvariantCulture));

        var request = new HttpRequestMessage
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", _weatherConfig.AppleDarkSkyKey) },
            Method = HttpMethod.Get,
            RequestUri = new Uri(url)
        };
        
        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error {@provider} request, status: {@statusCode}", providerName, response.StatusCode);
            return new WeatherInfo { RequestStatus = new RequestStatus(response.StatusCode) };
        }
        
        await using var content = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(content);
        var currentWeatherRoot = json.RootElement.GetProperty("currentWeather");
        var weatherInfo = JsonSerializer.Deserialize<WeatherInfo>(currentWeatherRoot.GetRawText());
        if (weatherInfo == null)
            return new WeatherInfo { RequestStatus = new RequestStatus(RequestStatusCode.Error) };
        var conditionCode = currentWeatherRoot.GetProperty("conditionCode").GetString() ?? "";
        var isDaylight = currentWeatherRoot.GetProperty("daylight").GetBoolean();
        var iconKey = conditionCode + Convert.ToInt32(isDaylight);
        if (conditionIcons.TryGetValue(conditionCode, out var icon)) 
            conditionIcons.TryGetValue(iconKey, out icon);
        weatherInfo.Icon = icon;
        if (json.RootElement.TryGetProperty("forecastHourly", out var forecastHoursRoot))
            weatherInfo.PrecipProbability = forecastHoursRoot
                .GetProperty("hours").EnumerateArray().First().GetProperty("precipitationChance").GetDecimal();
        weatherInfo.WindSpeed *= 0.277778M;
        weatherInfo.WeatherProvider = providerName;
        weatherInfo.RequestStatus = new RequestStatus(RequestStatusCode.Ok);
        return weatherInfo;
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
        var currentWeatherRoot = json.RootElement.GetProperty("current");
        var weatherInfo = JsonSerializer.Deserialize<WeatherInfo>(currentWeatherRoot.GetRawText());
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