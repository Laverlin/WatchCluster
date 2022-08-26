using Confluent.Kafka;
using Serilog;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using IB.WatchCluster.Abstract.Kafka.Entity;

namespace IB.WatchCluster.Abstract.Kafka;

public static class MessageExtensions
{
    public static bool TryParseHeader(this Message<string, string> message, out MessageHeader messageHeader)
    {
        messageHeader = null;

        if (!message.Headers.TryGetLastBytes("type", out var rawMessageType))
        {
            Log.Warning("Can not parse message without type");
            return false;
        }

        var messageType = SolutionInfo.Assembly.GetTypes().FirstOrDefault(
            t => t.Name == Encoding.ASCII.GetString(rawMessageType));
        if (messageType == null)
        {
            Log.Warning("Can not parse message of unknown type. Type {@rawMessageType} not found", rawMessageType);
            return false;
        }

        var activityId = message.Headers.TryGetLastBytes("activityId", out var rawActivityId)
            ? Encoding.ASCII.GetString(rawActivityId)
            : null;

        messageHeader = new MessageHeader
        {
            MessageType = messageType,
            ActivityId = activityId
        };
        return true;
    }

    public static bool TryParseMessageValue(
        this Message<string, string> rawMessage, Type messageType, out object messageValue)
    {
        messageValue = JsonSerializer.Deserialize(rawMessage.Value, messageType);
        if (messageValue == null)
            Log.Warning(
                "Can not parse message of type {@messageType}, raw message {@rawMessage}", 
                messageType, rawMessage.Value);

        return messageValue != null;
    }

    public static bool TryParseMessage(this Message<string, string> rawMessage, out KnownMessage knownMessage)
    {
        knownMessage = null;

        if (!rawMessage.TryParseHeader(out var messageHeader))
            return false;

        if (!rawMessage.TryParseMessageValue(messageHeader.MessageType, out var messageValue))
            return false;

        knownMessage = new KnownMessage
        {
            Key = rawMessage.Key,
            Header = messageHeader,
            Value = messageValue
        };
        return true;
    }
}