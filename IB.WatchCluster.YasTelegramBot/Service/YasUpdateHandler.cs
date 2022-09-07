using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using IB.WatchCluster.Abstract.Entity.SailingApp;
using IB.WatchCluster.YasTelegramBot.Infrastructure;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace IB.WatchCluster.YasTelegramBot.Service;

public class YasUpdateHandler: IUpdateHandler, IDisposable
{
    private readonly ILogger<YasUpdateHandler> _logger;
    private readonly HttpClient _httpClient;
    private readonly OtelMetrics _otelMetrics;
    private readonly Regex _renameLastRegex = new("/renamelast ([^;]+)", RegexOptions.IgnoreCase);
    private readonly Regex _renameRegex = new("/rename:([0-9]+) ([^;]+)", RegexOptions.IgnoreCase);
    private readonly Regex _deleteRegex = new("/delete:([0-9]+)", RegexOptions.IgnoreCase);

    private class CommandResult
    {
        public static CommandResult SuccessResult(string output, [CallerMemberName] string methodName = default!)
        {
            return new CommandResult
            {
                Output = output,
                Command = methodName,
                IsSuccess = true
            };
        }
        
        public static CommandResult ErrorResult(string output, [CallerMemberName] string methodName = default!)
        {
            return new CommandResult
            {
                Output = output,
                Command = methodName,
                IsSuccess = false
            };
        }

        public string Output { get; set; } = default!;
        public string Command { get; set; } = default!;
        public bool IsSuccess { get; set; }
    }

    public YasUpdateHandler(ILogger<YasUpdateHandler> logger, HttpClient httpClient, OtelMetrics otelMetrics)
    {
        _logger = logger;
        _httpClient = httpClient;
        _otelMetrics = otelMetrics;
    }
    
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        var commandResult = new CommandResult();
        try
        {
            if (update.Message == null)
                return;

            var yasUser = await GetYasUser(update.Message);
            if (yasUser == null)
                throw new ApplicationException($"Unable to find user with id {update.Message.From?.Id}");

            var output = update.Message switch
            {
                var msg when msg.Document != null => await CommandAddRoute(botClient, yasUser, msg.Document),
                var msg when msg.Text == "/start" => await CommandStart(),
                var msg when msg.Text == "/myid" => await CommandMyId(yasUser),
                var msg when msg.Text == "/list" => await CommandList(yasUser),
                var msg when _renameLastRegex.IsMatch(msg.Text!) =>
                    await CommandRenameLast(yasUser, _renameLastRegex.Match(msg.Text!).Groups[1].Value),
                var msg when _renameRegex.IsMatch(msg.Text!) =>
                    await CommandRename(
                        yasUser,
                        _renameRegex.Match(msg.Text!).Groups[1].Value,
                        _renameRegex.Match(msg.Text!).Groups[2].Value),
                var msg when _deleteRegex.IsMatch(msg.Text!) =>
                    await CommandDelete(yasUser, _deleteRegex.Match(msg.Text!).Groups[1].Value),
                _ => CommandResult.ErrorResult("Unknown command", "DefaultHandler")
            };

            await botClient.SendTextMessageAsync(
                update.Message.Chat, output.Output, ParseMode.Html,
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
        finally
        {
            _otelMetrics.IncrementCounter(new[]
            {
                new KeyValuePair<string, object?>("command", commandResult.Command),
                new KeyValuePair<string, object?>("is-success", commandResult.IsSuccess)
            });
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

    private async Task<CommandResult> CommandAddRoute(ITelegramBotClient botClient, YasUser yasUser, Document document)
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
                Latitude = Convert.ToDecimal(w.Attribute("lat")?.Value, CultureInfo.InvariantCulture),
                Longitude = Convert.ToDecimal(w.Attribute("lon")?.Value, CultureInfo.InvariantCulture),
                OrderId = orderId++
            }).ToList();
            
        if (points.Count == 0)
            return CommandResult.ErrorResult($"No route or way points were found in {fileName}");

        var route = new YasRoute
        {
            UserId = yasUser.UserId,
            UploadTime = DateTime.UtcNow,
            RouteName = root.Element(ns + "rte")?.Element(ns + "name")?.Value ?? Path.GetFileNameWithoutExtension(fileName),
            Waypoints = points.ToArray()
        };

        var url = $"{yasUser.PublicId}/route";
        var postResponse = await _httpClient.PostAsync(url, JsonContent.Create(route));

        if (!postResponse.IsSuccessStatusCode) 
            return CommandResult.ErrorResult($"Unable to upload {fileName}");
        
        var savedRoute = await postResponse.Content.ReadFromJsonAsync<YasRoute>();
        return CommandResult.SuccessResult(
            $"The route <b> {savedRoute?.RouteId} </b>:" +
            $" {route.RouteName} ({points.Count} way points) has been uploaded \n userId:{yasUser.PublicId}");
    }
    
    private async Task<CommandResult> CommandStart()
    {
        var output = CommandResult.SuccessResult(
            "/myid <code>- returns ID-string to identify your routes</code>\n\n" +
            "/list <code>- route list </code>\n\n" + "" +
            "/renamelast &lt;new name&gt; <code>- rename last uploaded route</code>\n\n " +
            "/rename:&lt;id&gt; &lt;new name&gt; <code>- set the &lt;new name&gt; to route with &lt;id&gt;</code>\n\n" +
            "/delete:&lt;id&gt; <code>delete route with &lt;id&gt;</code>");
        return await Task.FromResult(output);
    }

    private Task<CommandResult> CommandMyId(YasUser yasUser)
    {
        var output = CommandResult.SuccessResult(yasUser.PublicId);
        return Task.FromResult(output);
    }

    private async Task<CommandResult> CommandList(YasUser yasUser)
    {
        using var getResponse = await _httpClient.GetAsync($"{yasUser.PublicId}/route");
        var routes = await getResponse.Content.ReadFromJsonAsync<YasRoute[]>();
        var result = routes is { Length: > 0 }
            ? CommandResult.SuccessResult(routes.Aggregate("", (o, r) => 
                o + $"<b> {r.RouteId} </b> : <code>{r.RouteName} \n({r.UploadTime})</code>\n\n"))
            : CommandResult.ErrorResult("No routes found");
        return result;
    }

    private async Task<CommandResult> CommandRenameLast(YasUser yasUser, string newName)
    {
        var url = $"{yasUser.PublicId}/route?newName={newName}";
        using var putResponse = await _httpClient.PutAsync(url, null);
        return putResponse.IsSuccessStatusCode
            ? CommandResult.SuccessResult($"The route name has been successfully changed, the new name is {newName}")
            : CommandResult.ErrorResult("Unable to change route name");
    }
    
    private async Task<CommandResult> CommandRename(YasUser yasUser, string routeId, string newName)
    {
        var url = $"{yasUser.PublicId}/route/{routeId}?newName={newName}";
        using var putResponse = await _httpClient.PutAsync(url, null);
        return putResponse.IsSuccessStatusCode
            ? CommandResult.SuccessResult($"The route {routeId} name has been successfully changed, the new name is {newName}")
            : CommandResult.ErrorResult("Unable to change route name");
    }
    
    private async Task<CommandResult> CommandDelete(YasUser yasUser, string routeId)
    {
        var url = $"{yasUser.PublicId}/route/{routeId}";
        using var deleteResponse = await _httpClient.DeleteAsync(url);
        return deleteResponse.IsSuccessStatusCode
            ? CommandResult.SuccessResult($"The route {routeId} has been successfully deleted")
            : CommandResult.ErrorResult("Unable to delete route");
    }
    
    //private static string MethodName([CallerMemberName] string caller = default!) => caller;

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}