using System.Reactive.Linq;
using System.Reactive.Subjects;
using IB.WatchCluster.Abstract.Entity.WatchFace;
using IB.WatchCluster.Abstract.Kafka.Entity;

namespace IB.WatchCluster.Api.Services;

public class CollectorHandler
{
    private readonly ReplaySubject<KnownMessage> _messageSubject = new(TimeSpan.FromMinutes(1));

    public void OnNext(KnownMessage message) => _messageSubject.OnNext(message);
    public void OnCompleted() => _messageSubject.OnCompleted();
    public bool IsRunning { get; set; } = false;

    public virtual async Task<WatchResponse> GetCollectedMessages(string requestId)
    {
        var messages = await _messageSubject
            .Where(m => m.Key == requestId)
            .Take(3)
            .TakeUntil(Observable.Timer(TimeSpan.FromSeconds(15)))
            .ToArray();

        var response = new WatchResponse
        {
            RequestId = messages.Select(m => m.Key).FirstOrDefault(),
            LocationInfo = GetMessage<LocationInfo>(messages),
            WeatherInfo = GetMessage<WeatherInfo>(messages),
            ExchangeRateInfo = GetMessage<ExchangeRateInfo>(messages)
        };

        return response;
    }

    private static T GetMessage<T>(IEnumerable<KnownMessage> messages) where T : IHandlerResult, new() =>
        (T)(messages.SingleOrDefault(m => m.Header.MessageType.Name == typeof(T).Name)?.Value 
            ?? new T { RequestStatus = new RequestStatus(RequestStatusCode.Error) });
}