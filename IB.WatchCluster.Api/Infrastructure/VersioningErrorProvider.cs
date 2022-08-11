using IB.WatchCluster.Abstract.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;

namespace IB.WatchCluster.Api.Infrastructure
{
    public class VersioningErrorProvider : ErrorResponse, IErrorResponseProvider
    {
        /// <summary>
        /// Generate error response related to API versioning
        /// </summary>
        public IActionResult CreateResponse(ErrorResponseContext context)
        {
            return new ObjectResult(new ErrorResponse
            {
                StatusCode = context.StatusCode,
                StatusMessage = context.ErrorCode,
                Description = context.Message
            })
            {
                StatusCode = context.StatusCode
            };
        } 
    }
}