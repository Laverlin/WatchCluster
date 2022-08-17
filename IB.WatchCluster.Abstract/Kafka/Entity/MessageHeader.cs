using System;

namespace IB.WatchCluster.Abstract.Kafka.Entity;

public class MessageHeader
{
    public Type MessageType { get; set; }
    public string ActivityId { get; set; }
}