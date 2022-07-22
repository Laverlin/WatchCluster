using System;

namespace IB.WatchCluster.Abstract.Entity.Kafka
{
    public class MessageHeader
    {
        public Type MessageType { get; set; }
        public string ActivityId { get; set; }
    }
}
