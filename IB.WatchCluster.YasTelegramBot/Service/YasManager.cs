
using Confluent.Kafka;
using IB.WatchCluster.Abstract.Entity.SailingApp;
using IB.WatchCluster.Abstract.Kafka;
using Microsoft.Extensions.Logging;
using shortid.Configuration;

namespace IB.WatchCluster.YasTelegramBot.Service;

public class YasManager
{
    private readonly ILogger<YasManager> _logger;
    private readonly IKafkaBroker _kafkaBroker;

    public YasManager(ILogger<YasManager> logger, IKafkaBroker kafkaBroker)
    {
        _logger = logger;
        _kafkaBroker = kafkaBroker;
    }

    public async Task<YasUser> CreateUser(long telegramId, string userName)
    {
        var createParams = new
        {
            TelegramId = telegramId,
            UserName = userName,
            PublicId = shortid.ShortId.Generate(
                new GenerationOptions(useNumbers: true, useSpecialCharacters: false, length: 10))
        };
        var produce = await _kafkaBroker.ProduceYasMessageAsync("create-user", createParams);
        if (produce.Status != PersistenceStatus.Persisted)
            throw new ApplicationException("Unable to deliver create user message");
        
        return new YasUser
        {
            PublicId = createParams.PublicId,
            TelegramId = createParams.TelegramId,
            UserName = createParams.UserName,
            RegisterTime = new DateTime(),
        };
    }
    
    public async Task AddRoute<T>(T route)
    {
        var produce = await _kafkaBroker.ProduceYasMessageAsync("add-route", route);
        if (produce.Status != PersistenceStatus.Persisted)
            throw new ApplicationException("Unable to deliver add route message");
    }

    public async Task DeleteRoute(int routeId, long userId)
    {
        var deleteParams = new
        {
            routeId = routeId,
            userId = userId
        };
        var produce = await _kafkaBroker.ProduceYasMessageAsync("delete-route", deleteParams);
        if (produce.Status != PersistenceStatus.Persisted)
            throw new ApplicationException("Unable to deliver delete route message");
    }
    
    public async Task RenameRoute(int routeId, long userId, string newName)
    {
        var renameParams = new
        {
            routeId = routeId,
            userId = userId,
            newName = newName
        };
        var produce = await _kafkaBroker.ProduceYasMessageAsync("rename-route", renameParams);
        if (produce.Status != PersistenceStatus.Persisted)
            throw new ApplicationException("Unable to deliver rename route message");
    }
}