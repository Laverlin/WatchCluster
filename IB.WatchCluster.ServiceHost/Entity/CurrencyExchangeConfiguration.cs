using System.ComponentModel.DataAnnotations;


namespace IB.WatchCluster.ServiceHost.Entity
{
    public class CurrencyExchangeConfiguration
    {
        [Required, Url]
        public string ExchangeHostUrlTemplate { get; set; } = default!;

        [Required, Url]
        public string CurrencyConverterUrlTemplate { get; set; } = default!;

        [Required]
        public string CurrencyConverterKey { get; set; } = default!;
    }
}
