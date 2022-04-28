using System.Net;
using IB.WatchCluster.Abstract.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;


namespace IB.WatchCluster.Api.Infrastructure
{
    /// <summary>
    /// Throttling attribute.
    /// Prevent to request more that one request per "Second" from one "KeyField" client 
    /// KeyField could be in a query string or action parameter
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class RequestRateLimit : ActionFilterAttribute
    {
        private static readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
        private readonly ILogger<RequestRateLimit> _logger;

        /// <summary>
        /// The number of seconds during that subsequent requests from the same source will be prevented
        /// </summary>
        public int Seconds { get; set; }

        /// <summary>
        /// The name of the field to unique identify the request source
        /// </summary>
        public string KeyField { get; set; } = default!;

        public RequestRateLimit(ILogger<RequestRateLimit> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Actual execution
        /// </summary>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            
            string keyValue = string.Empty;
            if (context.ActionArguments.ContainsKey(KeyField))
                keyValue = context.ActionArguments[KeyField]?.ToString() ?? string.Empty;
            else if (context.HttpContext.Request.Query.ContainsKey(KeyField))
                keyValue = context.HttpContext.Request.Query[KeyField];
            if (string.IsNullOrEmpty(keyValue))
                return;

            string path = context.HttpContext.Request.Path;
            string memoryCacheKey = $"device-key:{keyValue}-{path}";
            _logger.LogDebug("{memoryCacheKey}", memoryCacheKey);

            if (!_memoryCache.TryGetValue(memoryCacheKey, out bool _))
            {
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromSeconds(Seconds));
                _memoryCache.Set(memoryCacheKey, true, cacheEntryOptions);
            }
            else
            {
                context.Result = new ObjectResult(
                    new ErrorResponse
                    {
                        StatusCode = (int) HttpStatusCode.TooManyRequests,
                        Description = $"Too many requests, retry after {Seconds}"
                    });

                context.HttpContext.Response.Headers.Add("Retry-After", Seconds.ToString());
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                _logger.LogWarning("Too many requests from {KeyValue}", keyValue);
            }
        }
    }
}
