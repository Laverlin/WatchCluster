using System.Text.Json.Serialization;

namespace IB.WatchCluster.Abstract.Entity
{
    /// <summary>
    /// Base contract for all api responses
    /// </summary>
    public abstract class BaseApiResponse
    {
        /// <summary>
        /// API Version number
        /// </summary>
        [JsonPropertyName("serverVersion")]
#pragma warning disable CA1822 // Mark members as static
        public string ServerVersion => SolutionInfo.Version;
    }
}
