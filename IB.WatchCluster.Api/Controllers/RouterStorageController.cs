using IB.WatchCluster.Abstract.Entity.SailingApp;
using IB.WatchCluster.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace IB.WatchCluster.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("3.0")]
[Produces("application/json")]
[Obsolete]
public class RouteStorageController
{
    private readonly ILogger<YaSailController> _logger;
    private readonly RouteHttpClient _routeHttpClient;

    public RouteStorageController(
        ILogger<YaSailController> logger, RouteHttpClient routeHttpClient)
    {
        _logger = logger;
        _routeHttpClient = routeHttpClient;
    }

    /// <summary>
    /// Return list of user's routes
    /// method: get /{userId}/routes 
    /// </summary>
    /// <param name="publicId">public user ID</param>
    /// <returns>JSON with all user's routes</returns>
    [HttpGet(template: "{publicId:length(7, 14)}/routes", Name = "RouteStorage")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [RequestRateFactory(KeyField = "publicId", Seconds = 2)]
    public async Task<ActionResult<IEnumerable<YasRoute>>> RouteList([FromRoute] string publicId)
    {
        return await _routeHttpClient.GetRoutes(publicId);
    }
}