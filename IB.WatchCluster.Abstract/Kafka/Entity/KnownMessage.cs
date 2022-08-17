
namespace IB.WatchCluster.Abstract.Kafka.Entity;

public class KnownMessage
{
    public string Key { get; set; }

    public MessageHeader Header { get; set; }

    public object Value { get; set; }
}