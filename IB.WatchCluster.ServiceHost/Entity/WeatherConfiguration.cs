using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.ServiceHost.Entity
{
    public class WeatherConfiguration
    {
        [Required, Url]
        public string DarkSkyUrlTemplate { get; set; } = default!;

        [Required, Url]
        public string OpenWeatherUrlTemplate { get; set; } = default!;

        [Required]
        public string OpenWeatherKey { get; set; } = default!;
    }
}
