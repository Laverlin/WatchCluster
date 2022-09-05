using IB.WatchCluster.Abstract.Database;
using IB.WatchCluster.Abstract.Entity.SailingApp;
using IB.WatchCluster.Api.Entity;
using IB.WatchCluster.Api.Infrastructure;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using shortid.Configuration;

namespace IB.WatchCluster.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0", Deprecated = true), ApiVersion("2.0")]
[Produces("application/json")]
public class YaSailController
{
    private readonly ILogger<YaSailController> _logger;
    private readonly DataConnectionFactory _dataConnectionFactory;

    public YaSailController(ILogger<YaSailController> logger, DataConnectionFactory dataConnectionFactory)
    {
        _logger = logger;
        _dataConnectionFactory = dataConnectionFactory;
    }
    
    /// <summary>
    /// Return user by telegram id
    /// resource: get /{telegramId}
    /// </summary>
    /// <param name="tId">telegram id</param>
    /// <returns>User data</returns>
    [HttpGet(template: "{tId:long}", Name = "GetUser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [TelemetryActivity("GetUser")]
    public async Task<ActionResult<YasUser>> GetUser([FromRoute] long tId)
    {
        await using var db = _dataConnectionFactory.Create();

        var yasUser = await db.GetTable<YasUser>().SingleOrDefaultAsync(u => u.TelegramId == tId);
        if (yasUser == null)
            throw new ApiException(StatusCodes.Status404NotFound, "User not found");
        return yasUser;
    }

    /// <summary>
    /// Add new user
    /// resource: post /
    /// </summary>
    /// <returns>User Id</returns>
    [HttpPost(Name = "AddUser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [TelemetryActivity("AddUser")]
    public async Task<ActionResult<YasUser>> AddUser([FromBody] YasUserCreateRequest yasUserCreateRequest)
    {
        await using var db = _dataConnectionFactory.Create();

        var yasUser = new YasUser
        {
            PublicId = shortid.ShortId.Generate(
                new GenerationOptions(useNumbers: true, useSpecialCharacters: false, length: 10)),
            TelegramId = yasUserCreateRequest.TelegramId,
            UserName = yasUserCreateRequest.UserName
        };

        yasUser.UserId = await db.GetTable<YasUser>().DataContext.InsertWithInt64IdentityAsync(yasUser);

        return new CreatedResult(yasUser.PublicId, null);
    }

    /// <summary>
    /// Return list of user's routes
    /// method: get /{userId}/route 
    /// </summary>
    /// <param name="publicId">public user ID</param>
    /// <returns>JSON with all user's routes</returns>
    [HttpGet(template: "{publicId:length(7, 14)}/route", Name = "RouteList")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestRateFactory(KeyField = "publicId", Seconds = 2)]
    [TelemetryActivity("GetRouteList")]
    public async Task<ActionResult<IEnumerable<YasRoute>>> RouteList([FromRoute] string publicId)
    {
        await using var db = _dataConnectionFactory.Create();
        
        var yasUser = db.GetTable<YasUser>().SingleOrDefault(u => u.PublicId == publicId);
        if (yasUser == null)
            throw new ApiException(StatusCodes.Status404NotFound, "User not found");

        var routes = db.GetTable<YasRoute>()
            .Where(r => r.UserId == yasUser.UserId)
            .OrderByDescending(r => r.UploadTime);
        var wayPoints = routes
            .Join(db.GetTable<YasWaypoint>(), r => r.RouteId, w => w.RouteId, (r, w) => w)
            .ToArray();
        var routesArray = routes.ToArray();
        foreach (var route in routesArray)
            route.Waypoints = wayPoints
                .Where(w => w.RouteId == route.RouteId)
                .OrderBy(w => w.OrderId)
                .ToArray();

        _logger.LogInformation(
            "Watch app request from User {@YasUser}, {RoutesCount} routes found", yasUser, routesArray.Length);
        return routesArray;
    }
    
    /// <summary>
    /// Add new route to the user base
    /// method: post /{userId}/route
    /// </summary>
    /// <param name="publicId">public user id</param>
    /// <param name="yasRoute"></param>
    /// <returns>Route ID</returns>
    [HttpPost(template: "{publicId:length(7, 14)}/route", Name = "AddRoute")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [TelemetryActivity("AddRoute")]
    public async Task<ActionResult> AddRoute(
        [FromRoute] string publicId, 
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] YasRoute yasRoute)
    {
        await using var db = _dataConnectionFactory.Create();

        var yasUser = await db.GetTable<YasUser>().SingleOrDefaultAsync(u => u.PublicId == publicId);
        if (yasUser == null)
            throw new ApiException(StatusCodes.Status404NotFound, "User not found");

        await db.BeginTransactionAsync();
        yasRoute.UserId = yasUser.UserId;
        yasRoute.RouteId = await db.GetTable<YasRoute>().DataContext.InsertWithInt64IdentityAsync(yasRoute);
        foreach (var wayPoint in yasRoute.Waypoints)
            wayPoint.RouteId = yasRoute.RouteId;
        await db.BulkCopyAsync(yasRoute.Waypoints);
        await db.CommitTransactionAsync();
        
        return new CreatedResult($"/Route/{yasRoute.RouteId}", yasRoute.RouteId);
    }
    
    /// <summary>
    /// Delete route
    /// resource: delete /{userid}/route{routeid}
    /// </summary>
    /// <returns>User Id</returns>
    [HttpDelete("{userId:length(7, 14)}/route/{routeId:int}", Name = "DeleteRoute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [TelemetryActivity("DeleteRoute")]
    public async Task<ActionResult> DeleteRoute([FromRoute]string userId, [FromRoute] int routeId)
    {
        await using var db = _dataConnectionFactory.Create();

        var deleted = await db.GetTable<YasRoute>()
            .Join(db.GetTable<YasUser>(), r => r.UserId, u => u.UserId, (r, u) => new {r, u})
            .Where(j => j.r.RouteId == routeId && j.u.PublicId == userId)
            .DeleteAsync();
        
        if (deleted != 1)
            throw new ApiException(StatusCodes.Status404NotFound, "Route not found");
        
        return new OkResult();
    }
    
    
    /// <summary>
    /// Rename route
    /// resource: PUT /{userid}/route{routeid}
    /// </summary>
    /// <returns>Ok 200</returns>
    [HttpPut("{userId:length(7, 14)}/route/{routeId:int}", Name = "RenameRoute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [TelemetryActivity("RenameRoute")]
    public async Task<ActionResult> RenameRoute([FromRoute]string userId, [FromRoute] long routeId, string newName)
    {
        await using var db = _dataConnectionFactory.Create();

        var updated = await db.GetTable<YasRoute>()
            .Join(db.GetTable<YasUser>(), r=>r.UserId, u => u.UserId, (r, u) => new {r, u})
            .Where(j => j.r.RouteId == routeId && j.u.PublicId == userId)
            .Set(j => j.r.RouteName, newName)
            .UpdateAsync();
        
        if (updated < 1)
            throw new ApiException(StatusCodes.Status404NotFound, "Route not found");
        
        return new OkResult();
    }

    /// <summary>
    /// Rename last added route
    /// resource: PUT /{userid}/route
    /// </summary>
    /// <returns>Ok 200</returns>
    [HttpPut("{userId:length(7, 14)}/route", Name = "RenameLastRoute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [TelemetryActivity("RenameLastRoute")]
    public async Task<ActionResult> RenameRoute([FromRoute]string userId, string newName)
    {
        await using var db = _dataConnectionFactory.Create();

        var route = await db.GetTable<YasRoute>()
            .Join(db.GetTable<YasUser>(), r => r.UserId, u => u.UserId, (r, u) => new { r, u })
            .Where(j => j.u.PublicId == userId)
            .OrderByDescending(j => j.r.RouteId)
            .Select(j => j.r)
            .FirstOrDefaultAsync();
  
        if (route == null)
            throw new ApiException(StatusCodes.Status404NotFound, "No route found");
        
        return await RenameRoute(userId, route.RouteId, newName);
    }


}