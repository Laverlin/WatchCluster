using System.ComponentModel.DataAnnotations;

namespace IB.WatchCluster.Api.Entity.Configuration
{
    /// <summary>
    /// Authentication settings
    /// </summary>
    public class AuthSettings
    {
        /// <summary>
        /// Name of the authentication scheme
        /// </summary>
        [Required]
        public string Scheme { get; set; } = default!;

        /// <summary>
        /// Name of the query parameter of token
        /// </summary>
        [Required]
        public string TokenName { get; set; } = default!;

        /// <summary>
        /// Token value
        /// </summary>
        [Required]
        public string Token { get; set; } = default!;
    }

}
