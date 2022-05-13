using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.ServiceHost.Entity
{
    public class VirtualEarthConfiguration
    {
        [Required, Url]
        public string UrlTemplate { get; set; } = default!;

        [Required]
        [StringLength(maximumLength: 64, MinimumLength = 64)]
        public string AuthKey { get; set; } = default!;
    }
}
