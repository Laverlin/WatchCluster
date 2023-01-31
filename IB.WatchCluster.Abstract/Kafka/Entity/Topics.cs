using System.Collections.Generic;

namespace IB.WatchCluster.Abstract.Kafka.Entity;

public static class Topics
{
    public static string RequestTopic => "watch-request";
    public static string ResponseTopic => "watch-response";
    public static IEnumerable<string> AllWfTopics => new[] { RequestTopic, ResponseTopic };
    public static string YasTopic => "yas-manager";
}