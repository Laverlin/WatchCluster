using System.Text.Json.Serialization;
using LinqToDB.Mapping;

namespace IB.WatchCluster.Abstract.Entity.SailingApp;

/// <summary>
/// Way point data
/// </summary>
[Table("yas_waypoint")]
public class YasWaypoint
{
    [Column("waypoint_id", IsIdentity = true)]
    [JsonPropertyName("waypointId")]
    public long WaypointId {get;set;}

    [Column("route_id")]
    [JsonPropertyName("routeId")]
    public long RouteId {get;set;}

    [Column("waypoint_name")]
    [JsonPropertyName("waypointName")]
    public string Name {get;set;}

    [Column("lat")]
    [JsonPropertyName("lat")]
    public decimal Latitude {get;set;}

    [Column("lon")]
    [JsonPropertyName("lon")]
    public decimal Longitude {get;set;}

    [Column("order_id")]
    [JsonIgnore]
    public int OrderId {get;set;}
}