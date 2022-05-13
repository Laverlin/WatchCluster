using System.Diagnostics.Metrics;

namespace IB.WatchCluster.ServiceHost.Infrastructure
{
    public class OtMetrics
    {
        public const string MetricName = "WatchCluster_sh";
        public Counter<long> LocationGetRemote { get; }
        public Counter<long> LocationGetCached { get; }

        public Counter<long> WeatherGetDarkSky { get; }
        public Counter<long> WeatherGetOpenWeather { get; }

        public Counter<long> ExchangeRateGetCached { get; }
        public Counter<long> ExchangeRateGetExchangeHost { get; }
        public Counter<long> ExchangeRateGetCurrencyConverter { get; }

        public Counter<long> MessageReceived { get; }
        public Counter<long> MessageProcessedTotal { get; }
        public Counter<long> MessageProcessedSuccess { get; }
        public Counter<long> MessageProcessedFail { get; }



        public OtMetrics()
        {
            var meter = new Meter(MetricName);
            /*
            foreach(var property in GetType().GetProperties())
            {
                property.SetValue(property, meter.CreateCounter<long>(MetricName + property.Name));
            }
            */
            LocationGetRemote = meter.CreateCounter<long>(MetricName + "_location_get_remote");
            LocationGetCached = meter.CreateCounter<long>(MetricName + "_location_get_cached");

            WeatherGetDarkSky = meter.CreateCounter<long>(MetricName + "_weather_get_darksky");
            WeatherGetOpenWeather = meter.CreateCounter<long>(MetricName + "_weather_get_openweather");

            ExchangeRateGetCached = meter.CreateCounter<long>(MetricName + "_exchange_get_cached");
            ExchangeRateGetExchangeHost = meter.CreateCounter<long>(MetricName + "_exchange_get_exchangehost");
            ExchangeRateGetCurrencyConverter = meter.CreateCounter<long>(MetricName + "_exchange_get_currencyconverter");

            MessageReceived = meter.CreateCounter<long>(MetricName + "_message_received");
            MessageProcessedTotal = meter.CreateCounter<long>(MetricName + "_message_processed_total");
            MessageProcessedSuccess = meter.CreateCounter<long>(MetricName + "_message_processed_success");
            MessageProcessedFail = meter.CreateCounter<long>(MetricName + "_messages_processed_fail");
        }
    }
}
