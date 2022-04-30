using IB.WatchCluster.Abstract.Entity;
using Microsoft.AspNetCore.Mvc;

namespace IB.WatchCluster.Api.Infrastructure
{
    public class ApiException : ApplicationException
    {
        public int HttpStatus { get; set; } = StatusCodes.Status500InternalServerError;
        public ErrorResponse ErrorResponse { get; set; } = new ErrorResponse
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            Description = "Internal Server Error"
        };

        public ApiException() : base() {}
        public ApiException(string message) : base(message) 
        {
            ErrorResponse.Description = message;
        }

        public ApiException(string message, Exception innerException) : base(message, innerException) 
        {
            ErrorResponse.Description = message + Environment.NewLine + innerException.Message.ToString();
        }

        public ApiException(int StatusCode, string message)
        {
            HttpStatus = StatusCode;
            ErrorResponse.StatusCode = StatusCode;
            ErrorResponse.Description = message; 
        }
    }

    public static class ErrorExtension
    {
        public static ObjectResult ReturnErrorResponse(this Exception exception)
        {

            if (exception is ApiException ex)
            {
                ObjectResult result = new(ex.ErrorResponse)
                {
                    StatusCode = ex.HttpStatus
                };
                return result;
            }
            return new(new ErrorResponse
            {
                StatusCode = StatusCodes.Status500InternalServerError,
                Description = $"Internal Error: {exception.Message}"
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
          }
    }
}
