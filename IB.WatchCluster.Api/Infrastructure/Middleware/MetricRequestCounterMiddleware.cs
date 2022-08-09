namespace IB.WatchCluster.Api.Infrastructure.Middleware
{
    public class MetricRequestCounterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly OtelMetrics _metrics;

        public MetricRequestCounterMiddleware(
            RequestDelegate next,
            OtelMetrics metrics)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _metrics = metrics;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
                RequestCountIncrement(context, context.Response.StatusCode);
            }
            catch (Exception)
            {
                RequestCountIncrement(context, StatusCodes.Status500InternalServerError);
                throw;
            }
        }

        private void RequestCountIncrement(HttpContext context, int statusCode)
        {
            _metrics.IncrementRequestCounter(new [] 
            {
                new KeyValuePair<string, object?>("status-code", statusCode),
                new KeyValuePair<string, object?>("endpoint-name", GetCurrentResourceName(context)),
                new KeyValuePair<string, object?>("endpoint", context.GetEndpoint()?.DisplayName),
                new KeyValuePair<string, object?>("is-websocket", context.WebSockets.IsWebSocketRequest)
            });
        }

        private string? GetCurrentResourceName(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            Endpoint? endpoint = httpContext.GetEndpoint();
            return endpoint?.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName;
        }
    }
}
