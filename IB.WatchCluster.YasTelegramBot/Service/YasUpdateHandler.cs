using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IB.WatchCluster.Abstract.Entity.SailingApp;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace IB.WatchCluster.YasTelegramBot.Service;

public class YasUpdateHandler: IUpdateHandler
{
    private readonly ILogger<YasUpdateHandler> _logger;
    private readonly HttpClient _httpClient;
    private readonly Regex _renameLastRegex = new("/renamelast ([^;]+)", RegexOptions.IgnoreCase);
    private readonly Regex _renameRegex = new("/rename:([0-9]+) ([^;]+)", RegexOptions.IgnoreCase);
    private readonly Regex _deleteRegex = new("/delete:([0-9]+)", RegexOptions.IgnoreCase);

    public YasUpdateHandler(ILogger<YasUpdateHandler> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }
    
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message == null)
                return;

            var yasUser = await GetYasUser(update.Message);
            if (yasUser == null)
                throw new ApplicationException($"Unable to find user with id {update.Message.From?.Id}");

            var output = update.Message switch
            {
                var msg when msg.Document != null => await MessageAddRoute(botClient, yasUser, msg.Document),
                var msg when msg.Text == "/start" => await MessageStart(),
                var msg when msg.Text == "/myid" => await MessageMyId(yasUser),
                var msg when msg.Text == "/list" => await MessageList(yasUser),
                var msg when _renameLastRegex.IsMatch(msg.Text!) =>
                    await MessageRenameLast(yasUser, _renameLastRegex.Match(msg.Text!).Groups[1].Value),
                var msg when _renameRegex.IsMatch(msg.Text!) =>
                    await MessageRename(
                        yasUser,
                        _renameRegex.Match(msg.Text!).Groups[1].Value,
                        _renameRegex.Match(msg.Text!).Groups[2].Value),
                var msg when _deleteRegex.IsMatch(msg.Text!) =>
                    await MessageDelete(yasUser, _deleteRegex.Match(msg.Text!).Groups[1].Value),
                _ => "Unknown command"
            };

            await botClient.SendTextMessageAsync(
                update.Message.Chat, output, ParseMode.Html,
                cancellationToken: cancellationToken);
            _logger.LogDebug("Message {@msg} from {@from}", update.Message.Text, update.Message.From);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to process message {@msg}, error:{@error}", 
                update.Message?.Text, exception.Message);
            await botClient.SendTextMessageAsync(
                update.Message!.Chat, "Unable to process the message", cancellationToken: cancellationToken);
        }
    }

    public Task HandlePollingErrorAsync(
        ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram bot exception was thrown: {@msg}", exception.Message);
        return Task.CompletedTask;
    }

    private async Task<YasUser?> GetYasUser(Message message)
    {
        var getResponse = await _httpClient.GetAsync($"{message.From?.Id}");
        if (getResponse.StatusCode == HttpStatusCode.NotFound)
        {
            var fullName = $"{message.From?.FirstName} ${message.From?.LastName}";
            var userName = !string.IsNullOrWhiteSpace(message.From?.Username)
                ? $"{message.From?.Username} ({fullName})"
                : fullName;
            
            var jsonContent = JsonContent.Create(new
            {
                TelegramId = message.From?.Id,
                UserName = userName
            });
            var postResponse = await _httpClient.PostAsync("", jsonContent);
            if (postResponse.StatusCode == HttpStatusCode.Created)
            {
                getResponse = await _httpClient.GetAsync($"{postResponse.Headers.Location}");
            }
        }

        if (!getResponse.IsSuccessStatusCode)
            return null;

        return await getResponse.Content.ReadFromJsonAsync<YasUser>();
    }

    private async Task<string> MessageAddRoute(ITelegramBotClient botClient, YasUser yasUser, Document document)
    {
        var fileId = document.FileId;
        var fileName = document.FileName;

        await using var memoryStream = new MemoryStream();  
        await botClient.GetInfoAndDownloadFileAsync(fileId, memoryStream);
        memoryStream.Position = 0;
        XNamespace ns = "http://www.topografix.com/GPX/1/1"; 
        var root = XElement.Load(memoryStream);
        var orderId = 0;
        var points = root.Elements(ns + "rte").Elements(ns + "rtept")
            .Union(root.Elements(ns + "wpt"))
            .Select(w => new YasWaypoint
            { 
                Name = w.Element(ns + "name")?.Value,
                Latitude = Convert.ToDecimal(w.Attribute("lat")?.Value),
                Longitude = Convert.ToDecimal(w.Attribute("lon")?.Value),
                OrderId = orderId++
            }).ToList();
            
        if (points.Count == 0)
            return $"No route or way points were found in {fileName}";

        var route = new YasRoute
        {
            UserId = yasUser.UserId,
            UploadTime = DateTime.UtcNow,
            RouteName = root.Element(ns + "rte")?.Element(ns + "name")?.Value ?? Path.GetFileNameWithoutExtension(fileName) 
        };

        var url = $"{yasUser.PublicId}/route";
        var postResponse = await _httpClient.PostAsync(url, JsonContent.Create(route));

        return postResponse.IsSuccessStatusCode
        ? $"The route <b> {route.RouteId} </b> : {route.RouteName} ({points.Count} way points) has been uploaded \n userId:{yasUser.PublicId}"
        : $"Unable to upload {fileName}";
    }
    
    private async Task<string> MessageStart()
    {
        var output = "/myid <code>- returns ID-string to identify your routes</code>\n\n" +
                     "/list <code>- route list </code>\n\n" + "" +
                     "/renamelast &lt;new name&gt; <code>- rename last uploaded route</code>\n\n " + 
                     "/rename:&lt;id&gt; &lt;new name&gt; <code>- set the &lt;new name&gt; to route with &lt;id&gt;</code>\n\n" + 
                     "/delete:&lt;id&gt; <code>delete route with &lt;id&gt;</code>";
        return await Task.FromResult(output);
    }

    private Task<string> MessageMyId(YasUser yasUser)
    {
        return Task.FromResult(yasUser.PublicId);
    }

    private async Task<string> MessageList(YasUser yasUser)
    {
        var getResponse = await _httpClient.GetAsync($"{yasUser.PublicId}/route");
        var routes = await getResponse.Content.ReadFromJsonAsync<YasRoute[]>();
        return routes is { Length: > 0 }
            ? routes.Aggregate("", (o, r) => o + $"<b> {r.RouteId} </b> : <code>{r.RouteName} \n({r.UploadTime})</code>\n\n")
            : "No routes found";
    }

    private async Task<string> MessageRenameLast(YasUser yasUser, string newName)
    {
        var url = $"{yasUser.PublicId}/route?newName={newName}";
        var putResponse = await _httpClient.PutAsync(url, null);
        return putResponse.IsSuccessStatusCode
            ? $"The route name has been successfully changed, the new name is {newName}"
            : "Can not change route name";
    }
    
    private async Task<string> MessageRename(YasUser yasUser, string routeId, string newName)
    {
        var url = $"{yasUser.PublicId}/route/{routeId}?newName={newName}";
        var putResponse = await _httpClient.PutAsync(url, null);
        return putResponse.IsSuccessStatusCode
            ? $"The route {routeId} name has been successfully changed, the new name is {newName}"
            : "Can not change route name";
    }
    
    private async Task<string> MessageDelete(YasUser yasUser, string routeId)
    {
        var url = $"{yasUser.PublicId}/route/{routeId}";
        var deleteResponse = await _httpClient.DeleteAsync(url);
        return deleteResponse.IsSuccessStatusCode
            ? $"The route {routeId} has been successfully deleted"
            : "Can not delete route";
    }
}