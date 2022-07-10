namespace IB.WatchCluster.Api.Infrastructure.Middleware
{
    public class ActiveRequestCounterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ActiveRequestCounterMiddleware> _logger;
        private readonly OtMetrics _metrics;

        public ActiveRequestCounterMiddleware(
            RequestDelegate next,
            ILogger<ActiveRequestCounterMiddleware> logger,
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
            _metrics.HttpRequestCount.Add(1);

            try
            {
                await _next(context);

                if (!(context.Response.StatusCode < StatusCodes.Status400BadRequest))
                {
                    _metrics.HttpErrorCount.Add(1, new KeyValuePair<string, object?>("statusCode", context.Response.StatusCode));
                }

            }
            catch (Exception ex)
            {
                _metrics.HttpErrorCount.Add(1, new KeyValuePair<string, object?>("statusCode", StatusCodes.Status500InternalServerError));

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
    }
}
