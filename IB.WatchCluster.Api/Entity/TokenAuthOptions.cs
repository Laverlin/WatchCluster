using Microsoft.AspNetCore.Authentication;

namespace IB.WatchCluster.Api.Entity
{
    /// <summary>
    /// Options for schema authentication
    /// </summary>
    public class TokenAuthOptions : AuthenticationSchemeOptions
    {
        /// <summary>
        /// Secret token provided by client
        /// </summary>
        public string ApiToken { get; set; } = default!;

        /// <summary>
        /// Scheme name
        /// </summary>
        public string Scheme { get; set; } = default!;

        /// <summary>
        /// Query parameter name where to find the token
        /// </summary>
        public string ApiTokenName { get; set; } = default!;
    }
}