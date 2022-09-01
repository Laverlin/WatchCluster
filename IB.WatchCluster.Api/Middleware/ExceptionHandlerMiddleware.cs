using System.Net;
using IB.WatchCluster.Abstract.Entity;
using IB.WatchCluster.Api.Infrastructure;

namespace IB.WatchCluster.Api.Middleware;

public class ExceptionHandlerMiddleware
{
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly RequestDelegate _next;

    public ExceptionHandlerMiddleware(ILogger<ExceptionHandlerMiddleware> logger, RequestDelegate next)
    {
        _logger = logger;
        _next = next;
    }
    
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(httpContext, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Handling exception: {@message}", exception.Message);

        var responseObject = exception switch
        {
            ApiException ex => new ErrorResponse
            {
                StatusCode = ex.HttpStatus,
                StatusMessage = "API Exception",
                Description = ex.Message
            },
            KeyNotFoundException ex => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                StatusMessage = "Not Found",
                Description = ex.Message 
            },
            _ => new ErrorResponse
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                StatusMessage = "Internal Server Error",
                Description = exception.Message
            }
        };
        
        var response = context.Response;
        response.StatusCode = responseObject.StatusCode;
        await response.WriteAsJsonAsync(responseObject);
    }
}