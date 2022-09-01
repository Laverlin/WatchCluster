namespace IB.WatchCluster.Api.Infrastructure;

public class ApiException : ApplicationException
{
    public int HttpStatus { get; set; } = StatusCodes.Status500InternalServerError;
        
    public ApiException() : base() {}
    
    public ApiException(string message) : base(message) {}

    public ApiException(string message, Exception innerException) : base(message, innerException) {}

    public ApiException(int statusCode, string message): base(message)
    {
        HttpStatus = statusCode;
    }
}