using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IB.WatchCluster.YasTelegramBot.Service;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Contrib.HttpClient;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Xunit;

namespace IB.WatchCluster.XUnitTest.UnitTests.BotTest;

public class BotTest
{
    [Fact]
    public async Task StatusShouldReturnStatus()
    {
        var baseUrl = "http://api-server/";
        var yasUserResponse =
            "{\"userId\":1,\"publicId\":\"bGXpKXWYd\",\"telegramId\":123456,\"userName\":null,\"registerTime\":\"0001-01-01T00:00:00Z\"}";
        
        var handler = new Mock<HttpMessageHandler>();
        var httpFactory = handler.CreateClientFactory();
        var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);

        var botClientMock = new Mock<ITelegramBotClient>();

        var update = new Update
        {
            Message = new Message
            {
                Text = "/start",
                Chat = new Chat
                {
                    Id = 123456
                },
                From = new User
                {
                    Id = 123456,
                    FirstName = "First"
                }
            }
        };

        handler
            .SetupRequest(HttpMethod.Get, new Uri($"{baseUrl}123456"))
            .ReturnsResponse(HttpStatusCode.OK, yasUserResponse);
        Mock<ILogger<YasUpdateHandler>> loggerMock = new Mock<ILogger<YasUpdateHandler>>();
        var updateHandler = new YasUpdateHandler(loggerMock.Object, client);

        await updateHandler.HandleUpdateAsync(botClientMock.Object, update, CancellationToken.None);

    }
    
    [Fact]
    public async Task IfUserNotExistsItShouldBeCreated()
    {
        var baseUrl = "http://api-server/";
        var yasUserResponse =
            "{\"userId\":1,\"publicId\":\"bGXpKXWYd\",\"telegramId\":123456,\"userName\":null,\"registerTime\":\"0001-01-01T00:00:00Z\"}";
        
        var handler = new Mock<HttpMessageHandler>();
        var httpFactory = handler.CreateClientFactory();
        var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);

        var botClientMock = new Mock<ITelegramBotClient>();

        var update = new Update
        {
            Message = new Message
            {
                Text = "/start",
                Chat = new Chat
                {
                    Id = 123456
                },
                From = new User
                {
                    Id = 123456,
                    FirstName = "First"
                }
            }
        };

        handler
            .SetupRequestSequence(HttpMethod.Get, new Uri($"{baseUrl}123456"))
            .ReturnsResponse(HttpStatusCode.NotFound)
            .ReturnsResponse(HttpStatusCode.OK, yasUserResponse);
        handler
            .SetupRequest(HttpMethod.Post, new Uri(baseUrl))
            .ReturnsResponse(
                HttpStatusCode.Created, c => c.Headers.Add("Location", "123456"));
        Mock<ILogger<YasUpdateHandler>> loggerMock = new Mock<ILogger<YasUpdateHandler>>();
        var updateHandler = new YasUpdateHandler(loggerMock.Object, client);

        await updateHandler.HandleUpdateAsync(botClientMock.Object, update, CancellationToken.None);
        
        handler.VerifyRequest(HttpMethod.Get, new Uri($"{baseUrl}123456"), Times.Exactly(2));
    }
}