using Microsoft.AspNetCore.Mvc.Filters;

namespace IB.WatchCluster.Api.Infrastructure
{
    /// <summary>
    /// The factory to provide parameters for <see cref="RequestRateLimit"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RequestRateFactoryAttribute : Attribute, IFilterFactory
    {
        /// <summary>
        /// The number of seconds during that subsequent requests from the same source will be prevented
        /// </summary>
        public int Seconds { get; set; }

        /// <summary>
        /// The name of the field to unique identify the request source
        /// </summary>
        public string KeyField { get; set; } = default!;

        public bool IsReusable => false;

        /// <summary>
        /// Creates the instance of the actual attribute
        /// </summary>
        /// <returns>filter attribute instance</returns>
        public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
        {
            var requestRateLimit = serviceProvider.GetRequiredService<RequestRateLimit>();
            requestRateLimit.KeyField = KeyField;
            requestRateLimit.Seconds = Seconds;
            return requestRateLimit;
        }
    }
}
