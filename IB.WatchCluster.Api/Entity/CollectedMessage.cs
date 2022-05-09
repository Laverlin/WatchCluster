namespace IB.WatchCluster.Api.Entity
{
    public class CollectedMessage
    {
        public string RequestId { get; set; } = default!;

        public string Message { get; set; } = default!;

        public string MessageType { get; set; } = default!;
    }
}
