namespace IB.WatchCluster.Api.Infrastructure.Middleware
{
    public class MetricRequestCounterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MetricRequestCounterMiddleware> _logger;
        private readonly OtMetrics _metrics;

        public MetricRequestCounterMiddleware(
            RequestDelegate next,
            ILogger<MetricRequestCounterMiddleware> logger,
            OtMetrics metrics)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger;
            _metrics = metrics;
        }

        public async Task Invoke(HttpContext context)
        {
            var isWebSocketRequest = context.WebSockets.IsWebSocketRequest;
            if (isWebSocketRequest)
            {
                _metrics.ActiveWSRequestCounter.Add(1);
            }
            else
            {
                _metrics.ActiveRequestCounter.Add(1);
            }

            try
            {
                await _next(context);

                requestCountIncrement(context, context.Response.StatusCode);
            }
            catch (Exception)
            {
                requestCountIncrement(context, StatusCodes.Status500InternalServerError);

                throw;
            }
            finally
            {
                if (isWebSocketRequest)
                {
                    _metrics.ActiveWSRequestCounter.Add(-1);
                }
                else
                {
                    _metrics.ActiveRequestCounter.Add(-1);
                }
            }
        }

        private void requestCountIncrement(HttpContext context, int statusCode)
        {
            _metrics.HttpRequestCount.Add(
                1,
                new KeyValuePair<string, object?>("statusCode", statusCode),
                new KeyValuePair<string, object?>("endpoint-name", getCurrentResourceName(context)),
                new KeyValuePair<string, object?>("endpoint", context.GetEndpoint()?.DisplayName));

            if (context.Response.StatusCode > StatusCodes.Status400BadRequest)
                _metrics.HttpErrorCount.Add(
                    1,
                    new KeyValuePair<string, object?>("statusCode", statusCode),
                    new KeyValuePair<string, object?>("endpoint-name", getCurrentResourceName(context)),
                    new KeyValuePair<string, object?>("endpoint", context.GetEndpoint()?.DisplayName));
        }

        private string? getCurrentResourceName(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            Endpoint? endpoint = httpContext.GetEndpoint();
            return endpoint?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName;
        }
    }
}
