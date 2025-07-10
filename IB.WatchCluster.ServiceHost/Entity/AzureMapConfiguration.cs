using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.ServiceHost.Entity
{
    public class AzureMapConfiguration
    {
        [Required, Url]
        public string UrlTemplate { get; set; } = default!;

        [Required]
        public string AuthKey { get; set; } = default!;
    }
}
