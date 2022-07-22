
namespace IB.WatchCluster.Abstract.Entity.Kafka
{
    public class KnownMessage
    {
        public string Key { get; set; }

        public MessageHeader Header { get; set; }

        public object Value { get; set; }
    }
}
