using System.Diagnostics;
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

public class YasUpdateHandler: IUpdateHandler
{
    private readonly ILogger<YasUpdateHandler> _logger;
    private readonly OtelMetrics _otelMetrics;
    private readonly ActivitySource _activitySource;
    private readonly YasHttpClient _yasHttpClient;
    private readonly YasManager _yasManager;
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

    public YasUpdateHandler(
        ILogger<YasUpdateHandler> logger, 
        OtelMetrics otelMetrics, 
        ActivitySource activitySource, 
        YasHttpClient yasHttpClient,
        YasManager yasManager)
    {
        _logger = logger;
        _otelMetrics = otelMetrics;
        _activitySource = activitySource;
        _yasHttpClient = yasHttpClient;
        _yasManager = yasManager;
    }
    
    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        Activity activity = _activitySource.StartActivity("/command", ActivityKind.Producer)!;
        var commandResult = new CommandResult();
        try
        {
            if (update.Message == null) return;
            
            var yasUser = await GetYasUser(update.Message);
            if (yasUser == null)
                throw new ApplicationException($"Unable to find user with id {update.Message.From?.Id}");

            commandResult = update.Message switch
            {
                var msg when msg.Document != null => await CommandAddRoute(botClient, yasUser, msg.Document),
                var msg when msg.Text == "/start" => await CommandStart(),
                var msg when msg.Text == "/myid" => await CommandMyId(yasUser),
                var msg when msg.Text == "/list" => await CommandList(yasUser),
                // var msg when _renameLastRegex.IsMatch(msg.Text!) =>
                //     await CommandRenameLast(yasUser, _renameLastRegex.Match(msg.Text!).Groups[1].Value),
                var msg when _renameRegex.IsMatch(msg.Text!) =>
                    await CommandRename(
                        yasUser,
                        _renameRegex.Match(msg.Text!).Groups[1].Value,
                        _renameRegex.Match(msg.Text!).Groups[2].Value),
                var msg when _deleteRegex.IsMatch(msg.Text!) =>
                    await CommandDelete(yasUser, _deleteRegex.Match(msg.Text!).Groups[1].Value),
                var msg when msg.Text?.EndsWith("#BoatingApp") ?? false => CommandResult.SuccessResult(""),
                _ => CommandResult.ErrorResult("Unknown command", "DefaultHandler")
            };

            if (!string.IsNullOrWhiteSpace(commandResult.Output))
                await botClient.SendTextMessageAsync(
                    update.Message.Chat, commandResult.Output, ParseMode.Html, cancellationToken: cancellationToken);
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
            if (!string.IsNullOrWhiteSpace(commandResult.Output))
                _otelMetrics.IncrementCounter(new[]
                {
                    new KeyValuePair<string, object?>("command", commandResult.Command),
                    new KeyValuePair<string, object?>("is-success", commandResult.IsSuccess)
                });
            activity.AddTag("command", commandResult.Command);
            activity.AddTag("is-success", commandResult.IsSuccess);
            activity.Stop();
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
        var tid = (message.From?.Id) ?? throw new ArgumentException("No telegram user id is provided");

        using var getResponse = await _yasHttpClient.GetUser(tid);
        
        if (getResponse.StatusCode == HttpStatusCode.NotFound)
        {
            var fullName = $"{message.From?.FirstName} {message.From?.LastName}";
            var userName = !string.IsNullOrWhiteSpace(message.From?.Username)
                ? $"{message.From?.Username} ({fullName})"
                : fullName;
            
            return await _yasManager.CreateUser(tid, userName);
        }

        if (!getResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Error requesting API server result: {@errorCode}", getResponse.StatusCode);
            return null;
        }

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
            Waypoints = points.OrderBy(wp => wp.OrderId).ToArray()
        };
        
        await _yasManager.AddRoute(route);
        
        return CommandResult.SuccessResult(
            $" {route.RouteName} ({points.Count} way points) has been uploaded \n userId:{yasUser.PublicId}");
    }
    
    private async Task<CommandResult> CommandStart()
    {
        var output = CommandResult.SuccessResult(
            "/myid <code>- returns ID-string to identify your routes</code>\n\n" +
            "/list <code>- route list </code>\n\n" + "" +
            // "/renamelast &lt;new name&gt; <code>- rename last uploaded route</code>\n\n " +
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
        using var getResponse = await _yasHttpClient.GetRouteList(yasUser.PublicId);
        if (getResponse.StatusCode == HttpStatusCode.NotFound)
                return CommandResult.ErrorResult("No routes found");
        
        var routes = await getResponse.Content.ReadFromJsonAsync<YasRoute[]>();
        var result = routes is { Length: > 0 }
            ? CommandResult.SuccessResult(routes.Aggregate("", (o, r) => 
                o + $"<b> {r.RouteId} </b> : <code>{r.RouteName} \n({r.UploadTime})</code>\n\n"))
            : CommandResult.ErrorResult("No routes found");
        return result;
    }

    // private async Task<CommandResult> CommandRenameLast(YasUser yasUser, string newName)
    // {
    //     var url = $"{yasUser.PublicId}/route?newName={newName}";
    //     using var putResponse = await _httpClient.PutAsync(url, null);
    //     return putResponse.IsSuccessStatusCode
    //         ? CommandResult.SuccessResult($"The route name has been successfully changed, the new name is {newName}")
    //         : CommandResult.ErrorResult("Unable to change route name");
    // }
    
    private async Task<CommandResult> CommandRename(YasUser yasUser, string routeId, string newName)
    {
        await _yasManager.RenameRoute(int.Parse(routeId), yasUser.PublicId, newName);
        return CommandResult.SuccessResult($"The route {routeId} name has been successfully changed, the new name is {newName}");
    }
    
    private async Task<CommandResult> CommandDelete(YasUser yasUser, string routeId)
    {
        await _yasManager.DeleteRoute(int.Parse(routeId), yasUser.PublicId);
        return CommandResult.SuccessResult($"The route {routeId} has been successfully deleted");
    }
}