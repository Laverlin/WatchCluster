using System.Net;
using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity.WatchFace
{
    /// <summary>
    /// Description of result of the request to an external service
    /// </summary>
    public class RequestStatus
    {
        public RequestStatus()
        {
        }

        public RequestStatus(RequestStatusCode statusCode)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// Use this constructor to create error status based on HttpStatusCode
        /// </summary>
        /// <param name="httpStatusCode">HttpStatusCode with error</param>
        public RequestStatus(HttpStatusCode httpStatusCode)
        {
            StatusCode = RequestStatusCode.Error;
            ErrorDescription = httpStatusCode.ToString();
            ErrorCode = (int)httpStatusCode;
        }

        /// <summary>
        /// Request status code. By default the status is request has not been done yet.
        /// </summary>
        [JsonPropertyName("statusCode")]
        public RequestStatusCode StatusCode { get; set; } = RequestStatusCode.HasNotBeenRequested;

        /// <summary>
        /// Text description of the error
        /// </summary>
        [JsonPropertyName("errorDescription")]
        public string ErrorDescription { get; set; }

        /// <summary>
        /// Error code. In most cases, http status code returned by external service
        /// </summary>
        [JsonPropertyName("errorCode")]
        public int ErrorCode { get; set; }
    }

    public enum RequestStatusCode
    {
        /// <summary>
        /// Unsuccessful
        /// </summary>
        Error = -1,

        /// <summary>
        /// The request has not been done yet
        /// </summary>
        HasNotBeenRequested = 0,

        /// <summary>
        /// The request was successful
        /// </summary>
        Ok = 1
    }
}
